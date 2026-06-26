namespace Zxc.Bot.Execution;

public interface IProcessExecutor
{
    Task<ProcessResult> ExecuteAsync(ProcessCommand command, CancellationToken cancellationToken);
}
