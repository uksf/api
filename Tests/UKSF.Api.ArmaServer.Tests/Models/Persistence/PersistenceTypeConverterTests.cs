using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using UKSF.Api.ArmaServer.Models.Persistence;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Models.Persistence;

public class PersistenceTypeConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new PersistenceTypeConverter() } };

    [Fact]
    public void Deserialize_Null_ReturnsNull()
    {
        var result = JsonSerializer.Deserialize<object>("null", Options);
        result.Should().BeNull();
    }

    [Fact]
    public void Deserialize_True_ReturnsBool()
    {
        var result = JsonSerializer.Deserialize<object>("true", Options);
        result.Should().Be(true);
    }

    [Fact]
    public void Deserialize_False_ReturnsBool()
    {
        var result = JsonSerializer.Deserialize<object>("false", Options);
        result.Should().Be(false);
    }

    [Fact]
    public void Deserialize_Integer_ReturnsLong()
    {
        var result = JsonSerializer.Deserialize<object>("42", Options);
        result.Should().BeOfType<long>();
        result.Should().Be(42L);
    }

    [Fact]
    public void Deserialize_LargeInteger_ReturnsLong()
    {
        var result = JsonSerializer.Deserialize<object>("10134600", Options);
        result.Should().BeOfType<long>();
        result.Should().Be(10134600L);
    }

    [Fact]
    public void Deserialize_Float_ReturnsDouble()
    {
        var result = JsonSerializer.Deserialize<object>("0.8", Options);
        result.Should().BeOfType<double>();
        result.Should().Be(0.8);
    }

    [Fact]
    public void Deserialize_String_ReturnsString()
    {
        var result = JsonSerializer.Deserialize<object>("\"hello\"", Options);
        result.Should().BeOfType<string>();
        result.Should().Be("hello");
    }

    [Fact]
    public void Deserialize_DateLikeString_ReturnsStringNotDateTime()
    {
        var result = JsonSerializer.Deserialize<object>("\"2026-03-16T12:00:00Z\"", Options);
        result.Should().BeOfType<string>();
        result.Should().Be("2026-03-16T12:00:00Z");
    }

    [Fact]
    public void Deserialize_EmptyArray_ReturnsEmptyList()
    {
        var result = JsonSerializer.Deserialize<object>("[]", Options);
        result.Should().BeOfType<List<object>>();
        ((List<object>)result).Should().BeEmpty();
    }

    [Fact]
    public void Deserialize_MixedArray_PreservesTypes()
    {
        var result = JsonSerializer.Deserialize<object>("""["ACRE_PRC343",8,0.8,true,null]""", Options);
        var arr = result.Should().BeOfType<List<object>>().Subject;
        arr[0].Should().BeOfType<string>().And.Be("ACRE_PRC343");
        arr[1].Should().BeOfType<long>().And.Be(8L);
        arr[2].Should().BeOfType<double>().And.Be(0.8);
        arr[3].Should().Be(true);
        arr[4].Should().BeNull();
    }

    [Fact]
    public void Deserialize_NestedArray_ReturnsNestedLists()
    {
        var result = JsonSerializer.Deserialize<object>("""[[1,2],[3]]""", Options);
        var outer = result.Should().BeOfType<List<object>>().Subject;
        outer.Should().HaveCount(2);
        var inner1 = outer[0].Should().BeOfType<List<object>>().Subject;
        inner1.Should().BeEquivalentTo(new object[] { 1L, 2L });
    }

    [Fact]
    public void Deserialize_Object_ReturnsDictionary()
    {
        var result = JsonSerializer.Deserialize<object>("""{"key":"value","num":42}""", Options);
        var dict = result.Should().BeOfType<Dictionary<string, object>>().Subject;
        dict["key"].Should().Be("value");
        dict["num"].Should().Be(42L);
    }

    [Fact]
    public void Deserialize_Zero_ReturnsLongNotDouble()
    {
        var result = JsonSerializer.Deserialize<object>("0", Options);
        result.Should().BeOfType<long>();
        result.Should().Be(0L);
    }

    [Fact]
    public void Deserialize_NegativeInteger_ReturnsLong()
    {
        var result = JsonSerializer.Deserialize<object>("-1", Options);
        result.Should().BeOfType<long>();
        result.Should().Be(-1L);
    }

    [Fact]
    public void RoundTrip_MixedArray_PreservesJson()
    {
        var json = """["test",42,0.5,true,null,[1,2]]""";
        var obj = JsonSerializer.Deserialize<object>(json, Options);
        var reserialized = JsonSerializer.Serialize(obj, Options);
        reserialized.Should().Be(json);
    }
}
