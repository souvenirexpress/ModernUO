using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

public static class LibertyLecturerBootstrap
{
    private static readonly Point3D BritainLectureLocation = new(1498, 1628, 10);

    public static void Configure() => EventSink.WorldLoad += EnsureLecturer;

    private static void EnsureLecturer()
    {
        LibertyLecturer lecturer = null;
        var duplicates = new List<LibertyLecturer>();

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is not LibertyLecturer candidate || candidate.Deleted)
            {
                continue;
            }

            if (lecturer == null)
            {
                lecturer = candidate;
            }
            else
            {
                duplicates.Add(candidate);
            }
        }

        foreach (var duplicate in duplicates)
        {
            duplicate.Delete();
        }

        lecturer ??= new LibertyLecturer();
        lecturer.MoveToWorld(BritainLectureLocation, Map.Felucca);
    }
}

[SerializationGenerator(0, false)]
public partial class LibertyLecturer : BaseCreature
{
    private static readonly string[] LectureLines =
    {
        "1/31 Willkommen. In den nächsten 15 Minuten betrachten wir Libertarismus, Ludwig von Mises und Friedrich August von Hayek - sachlich, mit Stärken und Einwänden.",
        "2/31 Libertarismus ist eine Familie politischer Ideen. Gemeinsam ist ihnen ein starker Vorrang individueller Freiheit gegenüber staatlichem oder kollektivem Zwang.",
        "3/31 Das Spektrum reicht vom klassischen Liberalismus und Minimalstaat bis zum Anarchokapitalismus. Über Aufgaben und sogar die Notwendigkeit des Staates besteht Streit.",
        "4/31 Ein häufiges Leitprinzip ist das Nichtaggressionsprinzip: Gewalt oder Betrug gegen friedliche Menschen gelten als unzulässig, Selbstverteidigung dagegen als erlaubt.",
        "5/31 Eigentumsrechte sollen Handlungsspielräume abgrenzen. Umstritten bleiben ihre ursprüngliche Begründung, gerechte Aneignung und der Umgang mit historisch belastetem Besitz.",
        "6/31 Freiwilliger Tausch gilt als beiderseitig vorteilhaft, sofern Zustimmung informiert und ohne Zwang erfolgt. Verträge und verlässliche Regeln tragen diese Kooperation.",
        "7/31 Libertäre misstrauen konzentrierter politischer Macht. Steuern, Verbote und Regulierung benötigen aus dieser Sicht eine besonders starke Rechtfertigung.",
        "8/31 Gegenüber dem breiteren Liberalismus setzt Libertarismus Freiheit und Eigentum meist strenger. Liberale akzeptieren oft mehr Sozialstaat, Umverteilung oder Regulierung.",
        "9/31 Ludwig von Mises, 1881 geboren, prägte die Österreichische Schule. Sein Ausgangspunkt war der handelnde Mensch, der Mittel für gewählte Ziele einsetzt.",
        "10/31 Mises nannte seine allgemeine Handlungslehre Praxeologie. Sie untersucht logische Strukturen zielgerichteten Handelns, nicht bloß statistische Regelmäßigkeiten.",
        "11/31 Wert ist bei Mises subjektiv: Ein Gut besitzt keinen festen wirtschaftlichen Wert. Menschen bewerten zusätzliche Einheiten nach ihren jeweiligen Zielen und Umständen.",
        "12/31 Daraus folgt die Grenznutzenlehre. Entscheidungen betreffen konkrete zusätzliche Einheiten; deshalb können Wasser insgesamt lebenswichtig und einzelne Diamanten dennoch teuer sein.",
        "13/31 Marktpreise bündeln Kauf- und Verkaufsentscheidungen. Sie sind keine moralischen Urteile, sondern veränderliche Signale über Knappheit und Zahlungsbereitschaft.",
        "14/31 Geldpreise ermöglichen Wirtschaftsrechnung: Unternehmer vergleichen erwartete Erlöse mit Kosten und prüfen, ob knappe Mittel anderswo dringender gebraucht werden.",
        "15/31 Im Kalkulationsargument behauptete Mises: Ohne Märkte für Produktionsmittel fehlen aussagekräftige Preise, um komplexe Produktionspläne wirtschaftlich zu vergleichen.",
        "16/31 Seine Sozialismuskritik war daher nicht nur ein Anreizargument. Sie fragte, wie eine Zentrale zwischen unzähligen technisch möglichen Plänen wählen kann.",
        "17/31 Auch Interventionen sah Mises kritisch: Preisgrenzen oder Einzelverbote können Folgen erzeugen, die weitere Eingriffe provozieren. Das ist eine Tendenz, kein Automatismus.",
        "18/31 Unternehmer handeln unter Unsicherheit. Gewinn belohnt bessere Erwartungen über künftige Bedürfnisse; Verlust zeigt, dass Ressourcen aus Sicht der Käufer fehlgeleitet wurden.",
        "19/31 Friedrich August von Hayek, 1899 geboren, teilte viele Marktargumente, setzte aber andere Akzente: verstreutes Wissen, institutionelle Entwicklung und Rechtsordnung.",
        "20/31 Hayeks Wissensproblem: Relevantes Wissen liegt nicht gesammelt vor. Es ist lokal, oft unausgesprochen und verändert sich fortwährend mit den Umständen.",
        "21/31 Das Preissystem kann dieses Wissen koordinieren. Ein steigender Preis signalisiert Knappheit, ohne dass jeder deren Ursache kennen oder einer Zentrale Bericht erstatten muss.",
        "22/31 Spontane Ordnung bezeichnet Muster, die aus vielen Handlungen entstehen, ohne vollständig entworfen zu sein - etwa Sprache, Gewohnheiten und wesentliche Teile von Märkten.",
        "23/31 Spontan bedeutet nicht automatisch gut. Institutionen können verbessert werden; Hayeks Warnung lautet, ihre komplexen Funktionen nicht durch vorschnelles Design zu zerstören.",
        "24/31 Freiheit braucht bei Hayek allgemeine, bekannte und verlässliche Regeln. Rechtsstaatlichkeit begrenzt Willkür, auch wenn einzelne Entscheidungen demokratisch beschlossen wurden.",
        "25/31 In 'Der Weg zur Knechtschaft' warnte Hayek, umfassende Wirtschaftsplanung könne politische Macht konzentrieren. Das Buch behauptet nicht, jeder Wohlfahrtsstaat werde zur Diktatur.",
        "26/31 Mises argumentiert stärker aus Handlungstheorie und Eigentum; Hayek stärker aus Wissen, Evolution und Verfassungsregeln. Beide sind verwandt, aber nicht austauschbar.",
        "27/31 Eine Stärke dieser Tradition ist ihre Aufmerksamkeit für unbeabsichtigte Folgen. Gute Absichten ersetzen keine Analyse von Anreizen, Wissen und Rückkopplungen.",
        "28/31 Kritiker nennen öffentliche Güter, externe Kosten und natürliche Monopole. Libertäre Antworten reichen von Haftungsrechten und Verträgen bis zu begrenzten Staatsaufgaben.",
        "29/31 Weitere Einwände betreffen Macht in privaten Beziehungen, ungleiche Startchancen und soziale Absicherung. Sie prüfen, wann formale Zustimmung tatsächlich frei genannt werden kann.",
        "30/31 Eine faire Prüfung fragt deshalb beides: Wo scheitert staatliche Steuerung an Macht und Wissen? Wo scheitern Märkte an Rechten, Anreizen oder fehlenden Alternativen?",
        "31/31 Fazit: Mises erklärt Wahl, Preise und Kalkulation; Hayek Wissen, Ordnung und Rechtsgrenzen. Weiterführend: 'Liberalismus', 'Der Gebrauch von Wissen' und kritische Gegenpositionen. Danke."
    };

    private Timer _lectureTimer;

    [Constructible]
    public LibertyLecturer() : base(AIType.AI_Animal, FightMode.None)
    {
        Name = "Professor Lysander";
        Title = "Dozent der Freiheit";
        Body = 0x190;
        Hue = Race.Human.RandomSkinHue();
        SpeechHue = 0x3B2;
        Direction = Direction.South;
        Blessed = true;
        CantWalk = true;

        InitStats(100, 100, 100);

        AddItem(new FancyShirt(0x47E));
        AddItem(new LongPants(0x59C));
        AddItem(new Cloak(0x455));
        AddItem(new Boots(0x1BB));
        Utility.AssignRandomHair(this, 0x455);
        Utility.AssignRandomFacialHair(this, 0x455);
    }

    public override bool ClickTitle => true;

    public override bool HandlesOnSpeech(Mobile from) => from.Alive && InRange(from, 12) || base.HandlesOnSpeech(from);

    public override void OnDoubleClick(Mobile from)
    {
        if (!from.Alive || !InRange(from, 3))
        {
            from.SendMessage("Du musst näher an den Dozenten herantreten.");
            return;
        }

        StartLecture(from);
    }

    public override void OnSpeech(SpeechEventArgs e)
    {
        base.OnSpeech(e);

        if (e.Handled || !e.Mobile.Alive || !InRange(e.Mobile, 12))
        {
            return;
        }

        var speech = e.Speech;

        if (_lectureTimer != null && (speech.InsensitiveContains("stopp") || speech.InsensitiveContains("stop lecture")))
        {
            e.Handled = true;
            StopLecture("Die Vorlesung wurde beendet. Mit 'Vorlesung' kannst du sie neu starten.");
            return;
        }

        if (speech.InsensitiveContains("vorlesung") ||
            speech.InsensitiveContains("libertarismus") ||
            speech.InsensitiveContains("mises") ||
            speech.InsensitiveContains("hayek"))
        {
            e.Handled = true;
            StartLecture(e.Mobile);
        }
    }

    public override bool CanBeDamaged() => false;

    public override void OnDelete()
    {
        _lectureTimer?.Stop();
        _lectureTimer = null;
        base.OnDelete();
    }

    private void StartLecture(Mobile from)
    {
        Direction = GetDirectionTo(from);

        if (_lectureTimer != null)
        {
            from.SendMessage("Die Vorlesung läuft bereits. Bleib in Hörweite oder sage 'Stopp'.");
            return;
        }

        from.SendMessage("Die 15-minütige Vorlesung beginnt. Sage 'Stopp', um sie zu beenden.");
        _lectureTimer = Timer.DelayCall(
            TimeSpan.Zero,
            TimeSpan.FromSeconds(30),
            LectureLines.Length,
            LectureTick
        );
    }

    private void LectureTick()
    {
        if (_lectureTimer == null || Deleted)
        {
            return;
        }

        var index = _lectureTimer.Index;
        if (index >= LectureLines.Length)
        {
            StopLecture(null);
            return;
        }

        PublicOverheadMessage(MessageType.Regular, SpeechHue, false, LectureLines[index]);

        if (index == LectureLines.Length - 1)
        {
            _lectureTimer = null;
        }
    }

    private void StopLecture(string message)
    {
        _lectureTimer?.Stop();
        _lectureTimer = null;

        if (message != null)
        {
            PublicOverheadMessage(MessageType.Regular, SpeechHue, false, message);
        }
    }
}
