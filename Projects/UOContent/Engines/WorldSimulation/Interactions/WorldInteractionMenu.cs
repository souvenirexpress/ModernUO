using System.Collections.Generic;
using Server.Items;

namespace Server.Engines.WorldSimulation;

public readonly record struct WorldInteractionOffer(WorldInteractionAction Action, Item Source);

public static class WorldInteractionMenu
{
    public static IReadOnlyList<WorldInteractionOffer> GetOffers(Mobile actor, Item target)
    {
        var offers = new List<WorldInteractionOffer>();
        if (actor == null || target is not IWorldSimulatedObject || !InteractionAccess.CanReach(actor, target))
        {
            return offers;
        }
        var sources = new List<Item>();
        var pending = new Stack<Item>();
        foreach (var item in actor.Items)
        {
            pending.Push(item);
        }
        // Bound inventory work even for deeply nested or unusually large backpacks.
        for (var visited = 0; pending.Count > 0 && visited < 128; visited++)
        {
            var item = pending.Pop();
            sources.Add(item);
            if (item is Container container)
            {
                for (var i = 0; i < container.Items.Count && pending.Count < 128; i++)
                {
                    pending.Push(container.Items[i]);
                }
            }
        }
        var nearby = 0;
        foreach (var item in actor.Map.GetItemsInRange<Item>(actor.Location, 2))
        {
            if (nearby++ >= 32)
            {
                break;
            }
            sources.Add(item);
        }

        foreach (var action in InteractionResolver.SupportedActions)
        {
            if (action == WorldInteractionAction.Inspect)
            {
                if (InteractionResolver.Explain(new InteractionContext { Actor = actor, Target = target, Action = action }).Allowed)
                {
                    offers.Add(new(action, null));
                }
                continue;
            }
            Item best = null;
            var bestPower = -1.0;
            foreach (var source in sources)
            {
                var context = new InteractionContext { Actor = actor, Source = source, Target = target, Action = action };
                if (!InteractionResolver.Explain(context).Allowed)
                {
                    continue;
                }
                var capabilities = SourceCapabilityResolver.GetCapabilities(source);
                var power = action switch
                {
                    WorldInteractionAction.Cut or WorldInteractionAction.Chop => MechanicalInteractionRule.EffectivePower(context),
                    WorldInteractionAction.Heat => capabilities.HeatPower,
                    WorldInteractionAction.Ignite => capabilities.IgnitePower,
                    WorldInteractionAction.Cool => capabilities.CoolPower,
                    _ => capabilities.ExtinguishPower
                };
                if (power > bestPower)
                {
                    best = source;
                    bestPower = power;
                }
            }
            if (best != null)
            {
                offers.Add(new(action, best));
            }
        }
        return offers;
    }
}
