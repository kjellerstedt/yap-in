using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using YapIn.Api.Responses;

namespace YapIn.IntegrationTests;

internal static class Assertions
{
    public static T AssertOk<T>(IResult result) => Assert.IsType<Ok<T>>(result).Value!;

    /// <returns>The error message the page is shown.</returns>
    public static string AssertError(IResult result, int expectedStatusCode)
    {
        Assert.Equal(expectedStatusCode, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        return Assert.IsAssignableFrom<IValueHttpResult<ErrorResponse>>(result).Value!.Error;
    }
}
