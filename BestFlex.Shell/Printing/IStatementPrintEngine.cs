using System.Windows.Documents;
using BestFlex.Application.Abstractions.Statements;

namespace BestFlex.Shell.Printing
{
    public interface IStatementPrintEngine
    {
        FlowDocument Create(StatementResult result);
    }
}
