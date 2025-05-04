using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Vip.RestClient;

public static class RestExtensions
{
    #region GET

    public static async Task<Response<T>> GetAsync<T>(this ClientApi client, string endpoint, int id)
    {
        return await client.GetAsync<T>($"{endpoint}/{id}");
    }

    public static async Task<Response<T>> GetAsync<T>(this ClientApi client, string endpoint, Guid id)
    {
        return await client.GetAsync<T>($"{endpoint}/{id}");
    }

    public static async Task<Response<T>> GetAsync<T>(this ClientApi client, string endpoint, KeyValuePair<string, string>[] values)
    {
        var url = Helper.BuildUrl(endpoint, values);
        return await client.GetAsync<T>(url);
    }

    public static async Task<Response<T>> GetAsync<T>(this ClientApi client, string endpoint, object args)
    {
        var url = Helper.BuildUrl(endpoint, Helper.BuildParams(args));
        return await client.GetAsync<T>(url);
    }

    #endregion

    #region POST

    public static async Task<Response<T>> PostAsync<T>(this ClientApi client, string endpoint, object value, int id)
    {
        return await client.PostAsync<T>($"{endpoint}/{id}", value);
    }

    public static async Task<Response<T>> PostAsync<T>(this ClientApi client, string endpoint, object value, Guid id)
    {
        return await client.PostAsync<T>($"{endpoint}/{id}", value);
    }

    public static async Task<Response> PostAsync(this ClientApi client, string endpoint, object value, int id)
    {
        return await client.PostAsync($"{endpoint}/{id}", value);
    }

    public static async Task<Response> PostAsync(this ClientApi client, string endpoint, object value, Guid id)
    {
        return await client.PostAsync($"{endpoint}/{id}", value);
    }

    #endregion

    #region MULTIPART POST

    public static async Task<Response<T>> MultipartFormPostAsync<T>(this ClientApi client, string endpoint, Dictionary<string, string> fields)
    {
        var lst = fields.Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value)).ToList();
        return await MultipartFormPostAsync<T>(client, endpoint, lst);
    }

    public static async Task<Response<T>> MultipartFormPostAsync<T>(this ClientApi client, string endpoint, NameValueCollection fields)
    {
        var lst = fields.AllKeys.Select(k => new KeyValuePair<string, string>(k, fields[k])).ToList();
        return await MultipartFormPostAsync<T>(client, endpoint, lst);
    }

    public static async Task<Response<T>> MultipartFormPostAsync<T>(this ClientApi client, string endpoint, IEnumerable<KeyValuePair<string, string>> fields)
    {
        var formContent = new MultipartFormDataContent();
        foreach (var item in fields) formContent.Add(new StringContent(item.Value), item.Key);

        return await client.PostAsync<T>(endpoint, formContent);
    }

    #endregion

    #region FORM URL ENCODED POST

    public static async Task<Response<T>> FormUrlEncodedPostAsync<T>(this ClientApi client, string endpoint, Dictionary<string, string> fields)
    {
        var lst = fields.Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value)).ToList();
        return await FormUrlEncodedPostAsync<T>(client, endpoint, lst);
    }

    public static async Task<Response<T>> FormUrlEncodedPostAsync<T>(this ClientApi client, string endpoint, NameValueCollection fields)
    {
        var lst = fields.AllKeys.Select(k => new KeyValuePair<string, string>(k, fields[k])).ToList();
        return await FormUrlEncodedPostAsync<T>(client, endpoint, lst);
    }

    public static async Task<Response<T>> FormUrlEncodedPostAsync<T>(this ClientApi client, string endpoint, IEnumerable<KeyValuePair<string, string>> fields)
    {
        var content = new FormUrlEncodedContent(fields);
        return await client.PostAsync<T>(endpoint, content);
    }

    public static async Task<Response<T>> FormUrlEncodedPostAsync<T>(this ClientApi client, string endpoint, object values) => await FormUrlEncodedPostAsync<T>(client, endpoint, Helper.BuildParams(values));

    #endregion

    #region PUT

    public static async Task<Response> PutAsync(this ClientApi client, string endpoint, object value, int id)
    {
        return await client.PutAsync($"{endpoint}/{id}", value);
    }

    public static async Task<Response> PutAsync(this ClientApi client, string endpoint, object value, Guid id)
    {
        return await client.PutAsync($"{endpoint}/{id}", value);
    }

    #endregion

    #region PATCH

    public static async Task<Response<T>> PatchAsync<T>(this ClientApi client, string endpoint, object value, int id)
    {
        return await client.PatchAsync<T>($"{endpoint}/{id}", value);
    }

    public static async Task<Response<T>> PatchAsync<T>(this ClientApi client, string endpoint, object value, Guid id)
    {
        return await client.PatchAsync<T>($"{endpoint}/{id}", value);
    }

    public static async Task<Response> PatchAsync(this ClientApi client, string endpoint, object value, int id)
    {
        return await client.PatchAsync($"{endpoint}/{id}", value);
    }

    public static async Task<Response> PatchAsync(this ClientApi client, string endpoint, object value, Guid id)
    {
        return await client.PatchAsync($"{endpoint}/{id}", value);
    }

    #endregion

    #region DELETE

    public static async Task<Response> DeleteAsync(this ClientApi client, string endpoint, int id)
    {
        return await client.DeleteAsync($"{endpoint}/{id}");
    }

    public static async Task<Response> DeleteAsync(this ClientApi client, string endpoint, Guid id)
    {
        return await client.DeleteAsync($"{endpoint}/{id}");
    }

    #endregion
}