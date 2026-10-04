using System;
using ModernUO.Serialization;
using Server.Engines.WorldSimulation;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public abstract partial class BaseWaterContainer : Container, IHasQuantity
{
    public BaseWaterContainer(int item_Id, bool filled) : base(item_Id) =>
        _quantity = filled ? MaxQuantity : 0;

    public abstract int EmptyItemId { get; }
    public abstract int FullItemId { get; }
    public abstract int MaxQuantity { get; }

    public override int DefaultGumpID => 0x3e;

    [CommandProperty(AccessLevel.GameMaster)]
    public virtual bool IsEmpty => _quantity <= 0;

    [CommandProperty(AccessLevel.GameMaster)]
    public virtual bool IsFull => _quantity >= MaxQuantity;

    [SerializableProperty(0)]
    [CommandProperty(AccessLevel.GameMaster)]
    public virtual int Quantity
    {
        get => _quantity;
        set
        {
            if (value != _quantity)
            {
                _quantity = Math.Clamp(value, 0, MaxQuantity);

                Movable = !IsLockedDown && IsEmpty;
                ItemID = IsEmpty ? EmptyItemId : FullItemId;

                if (!IsEmpty)
                {
                    var rootParent = RootParent;

                    if (rootParent?.Map != null && rootParent.Map != Map.Internal)
                    {
                        MoveToWorld(rootParent.Location, rootParent.Map);
                    }
                }

                InvalidateProperties();
                this.MarkDirty();
            }
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!from.InRange(GetWorldLocation(), 2))
        {
            from.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        if (IsEmpty)
        {
            base.OnDoubleClick(from);
            return;
        }

        from.BeginTarget(2, true, TargetFlags.None, PourOnTarget);
        from.SendMessage("What do you want to pour the water on?");
    }

    private void PourOnTarget(Mobile from, object target)
    {
        var result = InteractionResolver.Resolve(
            new InteractionContext
            {
                Actor = from,
                Source = this,
                Target = target,
                Action = target is IWorldSimulatedObject { State.IsBurning: true }
                    ? WorldInteractionAction.Extinguish : WorldInteractionAction.Cool,
                Environment = new EnvironmentContext()
            }
        );

        if (result.Success)
        {
            from.PlaySound(0x4E);
        }

        WorldSimulationMessaging.Send(from, result);
    }

    public override void OnSingleClick(Mobile from)
    {
        if (IsEmpty)
        {
            base.OnSingleClick(from);
        }
        else if (Name == null)
        {
            LabelTo(from, LabelNumber);
        }
        else
        {
            LabelTo(from, Name);
        }
    }

    public override void OnAosSingleClick(Mobile from)
    {
        if (IsEmpty)
        {
            base.OnAosSingleClick(from);
        }
        else if (Name == null)
        {
            LabelTo(from, LabelNumber);
        }
        else
        {
            LabelTo(from, Name);
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        if (IsEmpty)
        {
            base.GetProperties(list);
        }
    }

    public override bool OnDragDropInto(Mobile from, Item item, Point3D p) => IsEmpty && base.OnDragDropInto(from, item, p);
}
