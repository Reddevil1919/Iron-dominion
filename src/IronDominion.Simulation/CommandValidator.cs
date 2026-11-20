using IronDominion.Domain;

namespace IronDominion.Simulation;

internal sealed class CommandValidator(WorldEntities entities)
{
    private static readonly CommandResult Accepted = new(true, null);

    public CommandResult Validate(GameCommand command)
    {
        return command switch
        {
            MoveCommand move => ValidateUnits(move.Player, move.Units),
            AttackCommand attack when attack.Target.Value <= 0 => new(false, "invalid_target"),
            AttackCommand attack => ValidateUnits(attack.Player, attack.Units),
            TrainUnitCommand train when !Enum.IsDefined(train.Kind) => new(false, "invalid_unit_kind"),
            TrainUnitCommand train => ValidateProducer(train),
            _ => new(false, "unsupported_command")
        };
    }

    private CommandResult ValidateUnits(PlayerId player, IReadOnlyList<EntityId> units)
    {
        if (units.Count == 0)
            return new(false, "empty_selection");

        foreach (var id in units)
        {
            if (entities.Find(id) is not Unit unit)
                return new(false, "invalid_actor");

            var result = ValidateActor(player, unit.Owner, unit.HitPoints);
            if (!result.Accepted)
                return result;
        }

        return Accepted;
    }

    private CommandResult ValidateProducer(TrainUnitCommand command)
    {
        if (entities.Find(command.Producer) is not Building producer)
            return new(false, "invalid_actor");

        return ValidateActor(command.Player, producer.Owner, producer.HitPoints);
    }

    private static CommandResult ValidateActor(PlayerId player, PlayerId? owner, int hitPoints)
    {
        if (owner != player)
            return new(false, "not_owner");

        return hitPoints > 0 ? Accepted : new(false, "actor_destroyed");
    }
}
