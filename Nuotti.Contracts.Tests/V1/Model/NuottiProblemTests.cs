using System.Text.Json;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.Contracts.Tests.V1.Model;

public class NuottiProblemTests
{
    [Fact]
    public Task NuottiProblem_JSON_shape_snapshot()
    {
        var correlation = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");

        var cases = new[]
        {
            NuottiProblem.BadRequest(
                title: "Invalid input",
                detail: "The provided field is invalid.",
                reason: ReasonCode.InvalidStateTransition,
                field: "songIndex",
                correlationId: correlation
            ),
            NuottiProblem.Conflict(
                title: "Duplicate command",
                detail: "This operation was already performed.",
                reason: ReasonCode.DuplicateCommand,
                correlationId: correlation
            ),
            NuottiProblem.UnprocessableEntity(
                title: "Business rule violated",
                detail: "Performer cannot submit an answer.",
                reason: ReasonCode.UnauthorizedRole,
                field: "issuedByRole",
                correlationId: correlation
            )
        };

        var json = JsonSerializer.Serialize(cases, ContractsJson.DefaultOptions);
        return VerifyJson(json, VerifyDefaults.Settings());
    }

    [Fact]
    public void NuottiProblem_roundtrip_is_stable()
    {
        var sut = new NuottiProblem(
            Title: "Title",
            Status: 400,
            Detail: "Detail",
            Reason: ReasonCode.None,
            Field: null,
            CorrelationId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa")
        );

        var json1 = JsonSerializer.Serialize(sut, ContractsJson.DefaultOptions);
        var back = JsonSerializer.Deserialize<NuottiProblem>(json1, ContractsJson.DefaultOptions)!;
        var json2 = JsonSerializer.Serialize(back, ContractsJson.DefaultOptions);
        Assert.Equal(json1, json2);
    }
}
