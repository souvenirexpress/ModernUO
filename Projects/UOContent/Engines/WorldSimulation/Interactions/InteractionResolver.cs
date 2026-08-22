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
        new ExtinguishInteractionRule()
    ];

    public static InteractionResult Resolve(InteractionContext context)
    {
        foreach (var rule in _rules)
        {
            if (!rule.CanHandle(context))
            {
                continue;
            }

            var explanation = rule.Explain(context);
            return explanation.Allowed ? rule.Resolve(context) : InteractionResult.Failed(explanation.Reason);
        }

        return InteractionResult.Failed("No interaction rule can handle this action and target.");
    }

    public static InteractionExplanation Explain(InteractionContext context)
    {
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
