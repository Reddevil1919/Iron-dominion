using IronDominion.Domain;

namespace IronDominion.Application;

public sealed class PlayerCommands
{
    private readonly CommandScheduler _scheduler;

    internal PlayerCommands(CommandScheduler scheduler, PlayerId player)
    {
        _scheduler = scheduler;
        Player = player;
    }

    public PlayerId Player { get; }

    public void Move(EntityId unit, SimVector destination) => _scheduler.ScheduleMove(Player, [unit], destination);
    public void Attack(EntityId unit, EntityId target) => _scheduler.ScheduleAttack(Player, [unit], target);
    public void Train(EntityId producer, UnitKind kind) => _scheduler.ScheduleTraining(Player, producer, kind);
}
