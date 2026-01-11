using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BestFlex.Application.Abstractions.Statements
{
    public sealed record StatementFilter(
        string Customer,         // free text for now; later can be Id
        DateTime From,
        DateTime To,
        bool IncludeAging
    );

    public sealed record StatementLine(
        DateTime Date,
        string DocNo,
        string DocType,          // Invoice/Receipt/Adjustment...
        decimal Debit,           // increases balance
        decimal Credit,          // decreases balance
        decimal Balance,         // running balance after this line
        string? Notes
    );

    public sealed record AgingBuckets(decimal A0To30, decimal A31To60, decimal A61To90, decimal AOver90);

    public sealed record StatementResult(
        string Customer,
        DateTime From,
        DateTime To,
        decimal OpeningBalance,
        IReadOnlyList<StatementLine> Lines,
        decimal ClosingBalance,
        AgingBuckets? Aging
    );

    public interface ICustomerStatementService
    {
        Task<StatementResult> GetAsync(StatementFilter filter, CancellationToken ct = default);
    }
}
