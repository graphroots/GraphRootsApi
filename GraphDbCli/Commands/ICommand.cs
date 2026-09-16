using System.Threading;
using System.Threading.Tasks;

namespace GraphRoots.GraphDbCli
{
    internal interface ICommand
    {
        Task<int> Execute(CancellationToken cancellationToken = default);
    }
}
