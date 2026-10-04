namespace Server.Engines.WorldSimulation;

public static class InteractionAccess
{
    public static InteractionExplanation Explain(InteractionContext context)
    {
        if (context == null || context.Target == null)
        {
            return new(false, "Kein Ziel vorhanden.");
        }
        var target = (context.Target as IWorldSimulatedObject)?.Item ?? context.Target as Item;
        if (target?.Deleted != false)
        {
            return new(false, "Dieses Objekt existiert nicht mehr.");
        }
        if (context.Action != WorldInteractionAction.Inspect && ReferenceEquals(context.Source, context.Target))
        {
            return new(false, "Quelle und Ziel muessen verschieden sein.");
        }
        if (context.Source is Item { Deleted: true })
        {
            return new(false, "Das Werkzeug existiert nicht mehr.");
        }
        // A null actor is the deterministic, non-network simulation/test path.
        if (context.Actor is not { } actor)
        {
            return new(true, "");
        }
        if (actor.Deleted || !actor.Alive || actor.Frozen || actor.Paralyzed)
        {
            return new(false, "Ihr koennt gerade nicht handeln.");
        }
        if (context.Source is Server.Items.BaseLight { Protected: true } && actor.AccessLevel == AccessLevel.Player)
        {
            return new(false, "Diese Lichtquelle ist geschuetzt.");
        }
        if (!CanReach(actor, target))
        {
            return new(false, "Das Ziel ist nicht erreichbar oder nicht zugaenglich.");
        }
        if (context.Action != WorldInteractionAction.Inspect &&
            (context.Source is not Item source || !CanReach(actor, source)))
        {
            return new(false, "Das Werkzeug ist nicht erreichbar oder nicht zugaenglich.");
        }
        return new(true, "");
    }

    public static bool CanReach(Mobile actor, Item item) =>
        item?.Deleted == false && actor.Map != null && actor.Map != Map.Internal && item.Map == actor.Map &&
        (item.RootParent == null || item.RootParent == actor) &&
        Server.Multis.BaseHouse.CheckAccessible(actor, item) &&
        actor.InRange(item.GetWorldLocation(), 2) && actor.CanSee(item) && actor.InLOS(item) && item.IsAccessibleTo(actor);
}

public sealed class InspectInteractionRule : IInteractionRule
{
    public bool CanHandle(InteractionContext context) => context.Action == WorldInteractionAction.Inspect;
    public InteractionExplanation Explain(InteractionContext context) => new(true, "Das Objekt kann angesehen werden.");
    public InteractionResult Resolve(InteractionContext context)
    {
        var result = new InteractionResult { Success = true };
        if (context.Target is IWorldSimulatedObject target)
        {
            result.Messages.Add($"Material: {MaterialRegistry.Get(target.PrimaryMaterial).Name}; Zustand: {target.State.Condition:P0}; Temperatur: {target.State.Temperature:F0} C; Feuchte: {target.State.Moisture:P0}.");
        }
        else
        {
            result.Messages.Add("Dieses Objekt verwendet das klassische Objektsystem.");
        }
        return result;
    }
}
