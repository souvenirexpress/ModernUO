using Server.Engines.Seating;

namespace Server.Items;

public partial class FancyWoodenChairCushion
{
    public override void OnDoubleClick(Mobile from) => ChairSeating.TrySit(from, this);
}

public partial class WoodenChairCushion
{
    public override void OnDoubleClick(Mobile from) => ChairSeating.TrySit(from, this);
}

public partial class WoodenChair
{
    public override void OnDoubleClick(Mobile from) => ChairSeating.TrySit(from, this);
}

public partial class BambooChair
{
    public override void OnDoubleClick(Mobile from) => ChairSeating.TrySit(from, this);
}

public partial class StoneChair
{
    public override void OnDoubleClick(Mobile from) => ChairSeating.TrySit(from, this);
}

public partial class OrnateElvenChair
{
    public override void OnDoubleClick(Mobile from) => ChairSeating.TrySit(from, this);
}

public partial class BigElvenChair
{
    public override void OnDoubleClick(Mobile from) => ChairSeating.TrySit(from, this);
}

public partial class ElvenReadingChair
{
    public override void OnDoubleClick(Mobile from) => ChairSeating.TrySit(from, this);
}

public partial class Throne
{
    public override void OnDoubleClick(Mobile from) => ChairSeating.TrySit(from, this);
}

public partial class WoodenThrone
{
    public override void OnDoubleClick(Mobile from) => ChairSeating.TrySit(from, this);
}

public partial class Stool
{
    public override void OnDoubleClick(Mobile from) => ChairSeating.TrySit(from, this);
}

public partial class FootStool
{
    public override void OnDoubleClick(Mobile from) => ChairSeating.TrySit(from, this);
}

public partial class WoodenBench
{
    public override void OnDoubleClick(Mobile from) => ChairSeating.TrySit(from, this);
}
