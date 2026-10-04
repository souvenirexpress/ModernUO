using System.Collections.Generic;

namespace Server.Engines.WorldSimulation;

public static class AffordanceResolver
{
    public static IReadOnlyList<WorldInteractionAction> GetAvailableActions(
        Mobile actor,
        object source,
        object target,
        EnvironmentContext environment
    )
    {
        var actions = new List<WorldInteractionAction>();
        foreach (var action in InteractionResolver.SupportedActions)
        {
            if (InteractionResolver.Explain(new InteractionContext
                { Actor = actor, Source = source, Target = target, Action = action, Environment = environment }).Allowed)
            {
                actions.Add(action);
            }
        }

        return actions;
    }
}
