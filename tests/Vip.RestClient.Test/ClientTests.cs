using System;
using System.Threading.Tasks;
using Vip.RestClient.Test.Models;

namespace Vip.RestClient.Test;

public class ClientTests
{
    public static async Task Run()
    {
        #region Arrange

        Console.WriteLine("Início do teste de API");

        var guid = Guid.NewGuid();
        var data = new Data
        {
            Id = 1,
            Uid = Guid.NewGuid(),
            Text = "Teste de api",
            Number = 58,
            DoubleFloatPoint = 5.64
        };

        Console.WriteLine("");
        Console.WriteLine($"Model gerado - UID: {guid}");
        Console.WriteLine("");

        #endregion

        var client = new ClientApi("https://httpbin.org/");

        #region GET

        Console.WriteLine("GET - Sem parâmetros");
        WriteResponse(await client.GetAsync<Models.Response>("anything"));

        Console.WriteLine("GET - Parâmetro Inteiro");
        WriteResponse(await client.GetAsync<Models.Response>("anything", 12));

        Console.WriteLine("GET - Parâmetro GUID");
        WriteResponse(await client.GetAsync<Models.Response>("anything", guid));

        Console.WriteLine("GET - Parâmetro OBJETO");
        WriteResponse(await client.GetAsync<Models.Response>("anything", new {id = "1234", value = 12.34}));

        #endregion

        #region POST

        Console.WriteLine("POST (response) - Sem parâmetros");
        WriteResponse(await client.PostAsync<Models.Response>("anything", data));

        Console.WriteLine("POST (response) - Parâmetro Inteiro");
        WriteResponse(await client.PostAsync<Models.Response>("anything", data, 58));

        Console.WriteLine("POST (response) - Parâmetro GUID");
        WriteResponse(await client.PostAsync<Models.Response>("anything", data, guid));

        Console.WriteLine("POST (sem response) - Sem parâmetros");
        WriteResponse(await client.PostAsync("anything", data));

        Console.WriteLine("POST (sem response) - Parâmetro Inteiro");
        WriteResponse(await client.PostAsync("anything", data, 15));

        Console.WriteLine("POST (sem response) - Parâmetro GUID");
        WriteResponse(await client.PostAsync("anything", data, guid));

        #endregion

        #region PUT

        Console.WriteLine("PUT - Sem Parâmetros");
        WriteResponse(await client.PutAsync("anything", data));

        Console.WriteLine("PUT - Parâmetro inteiro");
        WriteResponse(await client.PutAsync("anything", data, 21));

        Console.WriteLine("PUT - Parâmetro GUID");
        WriteResponse(await client.PutAsync("anything", data, guid));

        #endregion

        #region DELETE

        Console.WriteLine("DELETE - Sem Parâmetros");
        WriteResponse(await client.DeleteAsync("anything"));

        Console.WriteLine("DELETE - Parâmetro inteiro");
        WriteResponse(await client.DeleteAsync("anything", 96));

        Console.WriteLine("DELETE - Parâmetro GUID");
        WriteResponse(await client.DeleteAsync("anything", guid));

        #endregion

        #region PATCH

        Console.WriteLine("PATCH - Sem Parâmetros");
        WriteResponse(await client.PatchAsync("anything", data));

        Console.WriteLine("PATCH - Parâmetro inteiro");
        WriteResponse(await client.PatchAsync("anything", data, 32));

        Console.WriteLine("PATCH - Parâmetro GUID");
        WriteResponse(await client.PatchAsync("anything", data, guid));

        #endregion

        Console.WriteLine("### Final de Teste ###");
    }

    private static void WriteResponse(Response<Models.Response> response)
    {
        Console.WriteLine($"Status: {response.StatusCode.GetDescription()}");
        Console.WriteLine($"Duration: {response.Duration}");
        Console.WriteLine($"Verbo: {response.RequestMessage.Method.Method}");
        Console.WriteLine($"URL: {response.RequestMessage.RequestUri?.AbsoluteUri}");
        Console.WriteLine($"Json: {response.Data?.json}");
        Console.WriteLine("");
    }

    private static void WriteResponse(Response response)
    {
        Console.WriteLine($"Status: {response.StatusCode.GetDescription()}");
        Console.WriteLine($"Duration: {response.Duration}");
        Console.WriteLine($"Verbo: {response.RequestMessage.Method.Method}");
        Console.WriteLine($"URL: {response.RequestMessage.RequestUri?.AbsoluteUri}");
        Console.WriteLine("");
    }
}