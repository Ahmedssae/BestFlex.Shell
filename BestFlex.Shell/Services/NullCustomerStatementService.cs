using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions.Statements;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Temporary stub that returns an empty statement (compiles & lets UI/printing work).
    /// Replace with EF-backed implementation later.
    /// </summary>
    public sealed class NullCustomerStatementService : ICustomerStatementService
    {
        public Task<StatementResult> GetAsync(StatementFilter filter, CancellationToken ct = default)
        {
            var lines = new List<StatementLine>();
            var res = new StatementResult(
                Customer: filter.Customer,
                From: filter.From,
                To: filter.To,
                OpeningBalance: 0m,
                Lines: lines,
                ClosingBalance: 0m,
                Aging: filter.IncludeAging ? new AgingBuckets(0, 0, 0, 0) : null
            );
            return Task.FromResult(res);
        }
    }
}
