using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class MobileCombatTrainingDummy : BaseCreature
{
    [Constructible]
    public MobileCombatTrainingDummy() : base(AIType.AI_Animal, FightMode.None)
    {
        Name = "Abadoria Trainingsziel";
        Body = 0x190;
        Hue = 0x83EA;
        CantWalk = true;
        SetStr(500);
        SetDex(50);
        SetInt(10);
        SetHits(5000);
        SetDamage(0);
        SetResistance(ResistanceType.Physical, 0);
        SetSkill(SkillName.Wrestling, 0.0);
        Fame = 0;
        Karma = -10000;
    }

    public override bool AlwaysMurderer => true;
    public override bool DeleteCorpseOnDeath => true;

    public override bool OnBeforeDeath()
    {
        Hits = HitsMax;
        return false;
    }
}
