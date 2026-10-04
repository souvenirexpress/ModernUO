using System.Collections.Generic;

namespace Server.Engines.WorldSimulation;

public interface IInteractionRule
{
    bool CanHandle(InteractionContext context);
    InteractionExplanation Explain(InteractionContext context);
    InteractionResult Resolve(InteractionContext context);
}

public static class InteractionResolver
{
    private static readonly List<IInteractionRule> _rules =
    [
        new HeatInteractionRule(),
        new IgniteInteractionRule(),
        new ExtinguishInteractionRule(),
        new MechanicalInteractionRule(),
        new InspectInteractionRule()
    ];

    public static readonly IReadOnlyList<WorldInteractionAction> SupportedActions = System.Array.AsReadOnly(new[]
    {
        WorldInteractionAction.Inspect, WorldInteractionAction.Cut, WorldInteractionAction.Chop,
        WorldInteractionAction.Heat, WorldInteractionAction.Ignite,
        WorldInteractionAction.Cool, WorldInteractionAction.Extinguish
    });

    public static InteractionResult Resolve(InteractionContext context)
    {
        var access = InteractionAccess.Explain(context);
        if (!access.Allowed)
        {
            return InteractionResult.Failed(access.Reason);
        }

        foreach (var rule in _rules)
        {
            if (!rule.CanHandle(context))
            {
                continue;
            }

            var explanation = rule.Explain(context);
            if (!explanation.Allowed)
            {
                return InteractionResult.Failed(explanation.Reason);
            }
            if (context.Actor != null && context.Action != WorldInteractionAction.Inspect)
            {
                if (!context.Actor.BeginAction(typeof(InteractionResolver)))
                {
                    return InteractionResult.Failed("Bitte wartet einen Moment.");
                }
                Timer.DelayCall(System.TimeSpan.FromMilliseconds(750), () => context.Actor.EndAction(typeof(InteractionResolver)));
            }
            return rule.Resolve(context);
        }

        return InteractionResult.Failed("No interaction rule can handle this action and target.");
    }

    public static InteractionExplanation Explain(InteractionContext context)
    {
        var access = InteractionAccess.Explain(context);
        if (!access.Allowed)
        {
            return access;
        }

        foreach (var rule in _rules)
        {
            if (rule.CanHandle(context))
            {
                return rule.Explain(context);
            }
        }

        return new InteractionExplanation(false, "No interaction rule can handle this action and target.");
    }
}
