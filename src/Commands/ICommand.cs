// 作用：定义命令的元数据与执行契约；不负责命令注册与业务实现。
namespace DT_Tools.Commands
{
    public interface ICommand
    {
        string Name { get; }

        string[] Aliases { get; }

        string Usage { get; }

        string Description { get; }

        string Author { get; }

        Core.Attributes.FeatureTag Tags { get; }

        bool RequireHost { get; }

        CommandResult Execute(CommandContext ctx);
    }
}
