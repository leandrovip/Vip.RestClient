using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Vip.RestClient.Tests.Utils;
using Vip.RestClient.Tests.Utils.Dtos;
using Xunit;

namespace Vip.RestClient.Tests;

public class JwtTests
{
    [Fact]
    public void Parse_text_decodes_three_synthetic_segments_without_signature_validation()
    {
        var token = JwtHelper.Token("{\"alg\":\"none\"}", "{\"Value\":\"payload\"}", new byte[] {1, 2, 255});
        var parsed = JwtBase.ParseText(token);
        Assert.Equal(token, parsed.OriginalToken);
        Assert.Equal("{\"alg\":\"none\"}", parsed.Header);
        Assert.Equal("{\"Value\":\"payload\"}", parsed.Payload);
        Assert.Equal(new byte[] {1, 2, 255}, parsed.Signature);
    }

    [Theory]
    [InlineData("")]
    [InlineData("one.two")]
    [InlineData("one.two.three.four")]
    public void Parse_text_rejects_empty_or_non_three_segment_tokens(string token)
    {
        Assert.Throws<ArgumentException>(() => JwtBase.ParseText(token));
    }

    [Theory]
    [InlineData("!.e30.c2ln")]
    [InlineData("e30.!.c2ln")]
    public void Invalid_header_or_payload_base64_decodes_to_legacy_null_reference(string token)
    {
        Assert.Throws<NullReferenceException>(() => JwtBase.ParseText(token));
    }

    [Fact]
    public void Invalid_signature_base64_is_null_without_validating_signature()
    {
        var parsed = JwtBase.ParseText("e30.e30.!");
        Assert.Null(parsed.Signature);
        Assert.Null(Jwt.Parse("e30.e30.!").Signature);
    }

    [Fact]
    public void Jwt_generic_parse_reads_payload_but_standard_short_claim_names_do_not_map()
    {
        var token = JwtHelper.Token("{}", "{\"iss\":\"issuer\",\"exp\":123,\"Value\":\"ok\"}", Array.Empty<byte>());
        var generic = Jwt<JwtClaims>.Parse(token);
        var ordinary = Jwt.Parse(token);

        Assert.Equal("ok", generic.Content.Value);
        Assert.Null(ordinary.Content.Issuer);
        Assert.Equal(0, ordinary.Content.ExpirationTime);
    }

    [Fact]
    public void Jwt_parse_maps_payload_properties_and_retains_all_token_parts()
    {
        const string header = "{\"alg\":\"none\"}";
        const string payload = "{\"Issuer\":\"synthetic-issuer\",\"ExpirationTime\":123,\"Subject\":\"synthetic-subject\"}";
        var signature = new byte[] {7, 8, 9};
        var token = JwtHelper.Token(header, payload, signature);

        var jwt = Jwt.Parse(token);

        Assert.Equal("synthetic-issuer", jwt.Content.Issuer);
        Assert.Equal(123L, jwt.Content.ExpirationTime);
        Assert.Equal("synthetic-subject", jwt.Content.Subject);
        Assert.Equal(header, jwt.Header);
        Assert.Equal(payload, jwt.Payload);
        Assert.Equal(signature, jwt.Signature);
        Assert.Equal(token, jwt.OriginalToken);
    }

    [Fact]
    public void Jwt_parsers_propagate_invalid_json_payload()
    {
        var token = JwtHelper.Token("{}", "not-json", Array.Empty<byte>());
        Assert.Throws<JsonReaderException>(() => Jwt.Parse(token));
        Assert.Throws<JsonReaderException>(() => Jwt<JwtClaims>.Parse(token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClientApi_special_cases_exact_Jwt_type_with_raw_or_quoted_token(bool quoted)
    {
        var token = JwtHelper.Token("{}", "{\"Value\":\"ok\"}", new byte[] {5});
        var body = quoted ? JsonConvert.SerializeObject(token) : token;
        var handler = new FakeHandler(_ => ResponseHelper.Text(body));
        using var harness = new ClientHarness(handler);

        var jwt = await harness.Client.GetAsync<Jwt>("token");
        Assert.Equal(token, jwt.Data.OriginalToken);
        Assert.Equal("{\"Value\":\"ok\"}", jwt.Data.Payload);
    }

    [Fact]
    public async Task ClientApi_does_not_special_case_Jwt_generic()
    {
        var token = JwtHelper.Token("{}", "{\"Value\":\"ok\"}", new byte[] {5});
        var handler = new FakeHandler(_ => ResponseHelper.Text(token));
        using var harness = new ClientHarness(handler);

        await Assert.ThrowsAsync<JsonReaderException>(() => harness.Client.GetAsync<Jwt<JwtClaims>>("token"));
    }
}