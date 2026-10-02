using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Vip.RestClient.Tests.Utils;
using Vip.RestClient.Tests.Utils.Models;
using Xunit;

namespace Vip.RestClient.Tests;

public class RestExtensionsTests
{
    [Fact]
    public async Task Id_extensions_append_integer_and_guid_path_segments()
    {
        var integerHandler = new FakeHandler(_ => ResponseHelper.Text("null"));
        using (var harness = new ClientHarness(integerHandler))
        {
            await harness.Client.GetAsync<string>("items", 42);
            Assert.Equal(new Uri("https://api.example.com/root/items/42"), integerHandler.RequestUri);
        }

        var id = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var guidHandler = new FakeHandler(_ => ResponseHelper.Text("ok"));
        using var guidHarness = new ClientHarness(guidHandler);
        await guidHarness.Client.PostAsync("items", new {Value = "x"}, id);
        Assert.Equal("POST", guidHandler.Method);
        Assert.Equal(new Uri("https://api.example.com/root/items/00112233-4455-6677-8899-aabbccddeeff"), guidHandler.RequestUri);
    }

    [Fact]
    public async Task Form_extensions_send_text_fields_as_url_encoded_and_multipart_content()
    {
        var values = new Dictionary<string, string> {["name"] = "hello world", ["kind"] = "synthetic"};
        var urlHandler = new FakeHandler(_ => ResponseHelper.Text("ok"));
        using (var harness = new ClientHarness(urlHandler))
        {
            await harness.Client.FormUrlEncodedPostAsync<string>("forms", values);
            Assert.Equal("name=hello+world&kind=synthetic", urlHandler.RequestBody);
            Assert.Contains("application/x-www-form-urlencoded", urlHandler.Request.Content.Headers.ContentType.ToString());
        }

        var multipartHandler = new FakeHandler(_ => ResponseHelper.Text("ok"));
        using var multipartHarness = new ClientHarness(multipartHandler);
        await multipartHarness.Client.MultipartFormPostAsync<string>("forms", values);
        Assert.Contains("name=name", multipartHandler.RequestBody);
        Assert.Contains("hello world", multipartHandler.RequestBody);
        Assert.Contains("synthetic", multipartHandler.RequestBody);
        Assert.Contains("multipart/form-data", multipartHandler.Request.Content.Headers.ContentType.ToString());
    }

    [Fact]
    public void Build_url_encodes_values_not_keys_and_always_adds_question_mark()
    {
        var pairs = new[] {new KeyValuePair<string, string>("raw key", "a b&c")};
        Assert.Equal("https://api.example.com/items?raw key=a+b%26c", Helper.BuildUrlEncodedUrl("https://api.example.com/items", pairs));
        Assert.Equal("/items?existing=1?next=two", Helper.BuildUrlEncodedUrl("/items?existing=1", new[] {new KeyValuePair<string, string>("next", "two")}));
    }

    [Fact]
    public void Build_url_from_null_object_is_empty_query_but_null_property_throws()
    {
        Assert.Equal("/items?", Helper.BuildUrlEncodedUrl("/items", (object) null));
        Assert.Throws<NullReferenceException>(() => Helper.BuildUrlEncodedUrl("/items", new NullableProperty {Value = null}));
    }

    [Fact]
    public void Numeric_decimal_float_double_are_invariant_but_other_values_keep_current_culture_ToString()
    {
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var query = Helper.BuildUrlEncodedUrl("/values", new CultureValues
            {
                Decimal = 12.5m,
                Float = 1.5f,
                Double = 2.5d,
                Date = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Unspecified),
                Optional = 17,
                Legacy = new LegacyValue()
            });

            Assert.Contains("Decimal=12.5", query);
            Assert.Contains("Float=1.5", query);
            Assert.Contains("Double=2.5", query);
            Assert.Contains("Date=02%2F01%2F2020", query);
            Assert.Contains("Optional=17", query);
            Assert.Contains("Legacy=legacy-value", query);
        }
        finally
        {
            CultureInfo.CurrentCulture = prior;
        }
    }
}