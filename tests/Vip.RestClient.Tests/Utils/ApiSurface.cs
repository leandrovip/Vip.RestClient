using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Vip.RestClient.Tests.Utils;

internal static class ApiSurface
{
    #region Métodos Públicos

    public static string[] Render(Assembly assembly)
    {
        var lines = new List<string>();
        foreach (var type in assembly.GetExportedTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            var kind = type.IsInterface ? "interface" : type.IsEnum ? "enum" : type.IsValueType ? "struct" : type.IsSubclassOf(typeof(Delegate)) ? "delegate" : "class";
            var marker = type.IsDefined(typeof(ExtensionAttribute), false) ? " extension" : "";
            var inheritance = type.BaseType == null ? "" : " : " + TypeName(type.BaseType);

            if (type.GetInterfaces().Length > 0)
                inheritance += (inheritance.Length == 0 ? " : " : ",") + string.Join(",", type.GetInterfaces().Select(TypeName).OrderBy(n => n, StringComparer.Ordinal));

            lines.Add("T " + Access(type) + " " + kind + " " + Name(type) + marker + inheritance + Constraints(type.GetGenericArguments()));

            foreach (var ctor in type.GetConstructors(Declared).Where(Visible).OrderBy(Key, StringComparer.Ordinal))
                lines.Add("C " + Access(ctor) + " (" + Parameters(ctor.GetParameters()) + ")");

            foreach (var field in type.GetFields(Declared).Where(Visible).OrderBy(f => f.Name, StringComparer.Ordinal))
                lines.Add("F " + Access(field) + (field.IsStatic ? " static" : " instance") + (field.IsInitOnly ? " readonly" : "") + (field.IsLiteral ? " const" : "") + " " + TypeName(field.FieldType) + " " + field.Name);

            foreach (var property in type.GetProperties(Declared).Where(p => Visible(p.GetMethod) || Visible(p.SetMethod)).OrderBy(p => p.Name, StringComparer.Ordinal).ThenBy(Key, StringComparer.Ordinal))
            {
                var accessor = Visible(property.GetMethod) ? property.GetMethod : property.SetMethod;
                var accessors = (Visible(property.GetMethod) ? "get:" + Access(property.GetMethod) + ";" : "") + (Visible(property.SetMethod) ? "set:" + Access(property.SetMethod) + ";" : "");
                lines.Add("P " + Access(accessor) + (accessor.IsStatic ? " static" : " instance") + " " + TypeName(property.PropertyType) + " " + property.Name + (property.GetIndexParameters().Length == 0 ? "" : "[" + Parameters(property.GetIndexParameters()) + "]") + " {" + accessors + "}");
            }

            foreach (var evt in type.GetEvents(Declared).Where(e => Visible(e.AddMethod) || Visible(e.RemoveMethod)).OrderBy(e => e.Name, StringComparer.Ordinal))
                lines.Add("E " + Access(evt.AddMethod ?? evt.RemoveMethod) + " " + TypeName(evt.EventHandlerType) + " " + evt.Name + " {add:" + Access(evt.AddMethod) + ";remove:" + Access(evt.RemoveMethod) + ";}");

            foreach (var method in type.GetMethods(Declared).Where(m => Visible(m) && (!m.IsSpecialName || m.Name.StartsWith("op_", StringComparison.Ordinal))).OrderBy(Key, StringComparer.Ordinal))
                lines.Add("M " + Access(method) + (method.IsStatic ? " static" : " instance") + (method.IsAbstract ? " abstract" : method.IsVirtual ? method.IsFinal ? " sealed-virtual" : " virtual" : "") + " " + TypeName(method.ReturnType) + " " + method.Name +
                          GenericNames(method.GetGenericArguments()) + "(" + Parameters(method.GetParameters()) + ")" + (method.IsDefined(typeof(ExtensionAttribute), false) ? " extension" : "") + Constraints(method.GetGenericArguments()));
        }

        return [.. lines];
    }

    #endregion

    #region Métodos Privados

    private static bool Visible(MethodBase method)
    {
        return method != null && (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly);
    }

    private static bool Visible(FieldInfo field)
    {
        return field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly;
    }

    private static string Access(MemberInfo member)
    {
        var method = member as MethodBase;
        var field = member as FieldInfo;
        var isPublic = method != null ? method.IsPublic : field != null ? field.IsPublic : ((Type) member).IsPublic || ((Type) member).IsNestedPublic;
        if (isPublic) return "public";
        if ((method != null && method.IsFamilyOrAssembly) || (field != null && field.IsFamilyOrAssembly)) return "protected-internal";
        return "protected";
    }

    private static string Key(MethodBase method)
    {
        return method.Name + "`" + (method is MethodInfo info ? info.GetGenericArguments().Length : 0).ToString(CultureInfo.InvariantCulture) + "(" + Parameters(method.GetParameters()) + ")";
    }

    private static string Key(PropertyInfo property)
    {
        return property.Name + "(" + Parameters(property.GetIndexParameters()) + ")";
    }

    private static string Parameters(ParameterInfo[] parameters)
    {
        return string.Join(",", parameters.Select(p => (p.IsOut ? "out " : p.IsIn ? "in " : p.ParameterType.IsByRef ? "ref " : "") + TypeName(p.ParameterType) + " " + p.Name + (p.IsOptional ? "=" + DefaultValue(p.DefaultValue) : "")));
    }

    private static string DefaultValue(object value)
    {
        return value == null ? "null" : value is string ? "\"" + value + "\"" : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static string GenericNames(Type[] arguments)
    {
        return arguments.Length == 0 ? "" : "<" + string.Join(",", arguments.Select(a => a.Name)) + ">";
    }

    private static string Constraints(Type[] arguments)
    {
        var result = new StringBuilder();
        foreach (var argument in arguments.Where(a => a.IsGenericParameter))
        {
            var attrs = argument.GenericParameterAttributes & GenericParameterAttributes.SpecialConstraintMask;
            var constraints = new List<string>();
            if ((attrs & GenericParameterAttributes.ReferenceTypeConstraint) != 0) constraints.Add("class");
            if ((attrs & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0) constraints.Add("struct");
            constraints.AddRange(argument.GetGenericParameterConstraints().Select(TypeName));
            if ((attrs & GenericParameterAttributes.DefaultConstructorConstraint) != 0) constraints.Add("new()");
            if (constraints.Count > 0) result.Append(" where ").Append(argument.Name).Append(" : ").Append(string.Join(",", constraints));
        }

        return result.ToString();
    }

    private static string Name(Type type)
    {
        var name = type.FullName.Replace('+', '.');
        if (!type.IsGenericTypeDefinition) return name;
        var tick = name.IndexOf('`');
        if (tick >= 0) name = name.Substring(0, tick);
        return name + GenericNames(type.GetGenericArguments());
    }

    private static string TypeName(Type type)
    {
        if (type.IsByRef) return TypeName(type.GetElementType()) + "&";
        if (type.IsArray) return TypeName(type.GetElementType()) + "[]";
        if (type.IsGenericParameter) return type.Name;
        if (!type.IsGenericType) return Name(type);
        var name = type.GetGenericTypeDefinition().FullName;
        var tick = name.IndexOf('`');
        if (tick >= 0) name = name.Substring(0, tick);
        return name.Replace('+', '.') + "<" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + ">";
    }

    #endregion

    #region Constantes

    private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    #endregion
}