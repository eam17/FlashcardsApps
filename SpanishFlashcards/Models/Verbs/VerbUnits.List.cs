namespace SpanishFlashcards.Models.Verbs;

public static partial class VerbUnits
{
    /// <summary>
    /// The units, in learning order. Texts use the rules markup: *Spanish*, **bold**. No dashes as punctuation in
    /// anything shown.
    /// </summary>
    public static readonly IReadOnlyList<UnitDef> Defs =
    [
        // ---------------------------------------------------------------- tier 1: now, plans, what happened
        new("pres-ar", 1, "Present: -ar verbs",
            "Take off *-ar* and add an ending that says who: *hablo* (I speak), *hablas*, *habla*, *hablamos*, *hablan*. Most Spanish verbs end in *-ar*, so this one pattern goes a long way.",
            UnitGoal.Use, UnitKind.Pattern, [S("pres", "ar")], []),

        new("ser-estar", 1, "ser and estar",
            "Both mean \"to be\". *ser* is what something is: *soy de Lima*, *es grande*. *estar* is where it is or how it is right now: *estoy en Cusco*, *está cansado*. Both are irregular, so learn them as whole verbs.",
            UnitGoal.Use, UnitKind.Memorise, [], [W(["ser", "estar"], "pres")],
            Tip: "*usted* uses the *él* form: *¿Usted es de aquí?* (Are you from here?). *ustedes* uses the *ellos* form: *¿Ustedes están bien?*"),

        new("pres-er-ir", 1, "Present: -er and -ir verbs",
            "The same idea with *e* instead of *a*: *como, comes, come, comemos, comen*. *-ir* verbs only differ in *nosotros* and *vosotros*: *vivimos*.",
            UnitGoal.Use, UnitKind.Pattern, [S("pres", "er"), S("pres", "ir")], []),

        new("ir-going", 1, "ir, and going to",
            "*ir* (to go) is irregular: *voy, vas, va, vamos, van*. Add *a* and any infinitive to talk about plans: *voy a comer* (I'm going to eat). In Latin America this is the usual way to talk about the future.",
            UnitGoal.Use, UnitKind.Group, [S("near", "inf")], [W(["ir"], "pres")]),

        new("pres-go", 1, "tener, hacer and the -go verbs",
            "In the present, *yo* ends in *-go*: *tengo, hago, digo, pongo, salgo, vengo*. *tener*, *venir* and *decir* also change their stem in other persons: *tienes, vienes, dices*.",
            UnitGoal.Use, UnitKind.Memorise, [], [W(["tener", "hacer", "decir", "poner", "salir", "venir"], "pres")]),

        new("pres-stem", 1, "Stem changes: e → ie, o → ue",
            "In some verbs the stressed vowel splits: *querer* → *quiero*, *pensar* → *pienso*, *poder* → *puedo*, *volver* → *vuelvo*. Not in *nosotros* and *vosotros*, where the stress moves to the ending: *queremos*, *podemos*.",
            UnitGoal.Use, UnitKind.Group, [S("pres", "e-ie"), S("pres", "o-ue")], []),

        new("polite", 1, "Polite requests",
            "Set phrases for asking politely, to learn as they are. The grammar behind them (the conditional and the imperfect subjunctive) comes later.",
            UnitGoal.Use, UnitKind.Phrases, [], [],
            Tip: "*Quisiera* and *me gustaría* both mean \"I'd like\". *¿Podría…?* (could you…?) goes with *usted*, the polite way to talk to people you don't know.",
            Idea:
            [
                "*Quisiera…*: I'd like…",
                "*Me gustaría…*: I'd like… (also: I'd like to…)",
                "*¿Podría…?*: Could you…? (to *usted*)",
                "*¿Me podría…?*: Could you… for me?",
                "*¿Sería posible…?*: Would it be possible…?",
            ]),

        new("pret-ar", 1, "Preterite: -ar verbs",
            "For one finished action in the past: *hablé* (I spoke), *hablaste*, *habló*, *hablamos*, *hablaron*. Watch the accents: *hablo* is I speak, *habló* is he spoke.",
            UnitGoal.Use, UnitKind.Pattern, [S("pret", "ar"), S("pret", "car-que"), S("pret", "gar-gue"), S("pret", "zar-ce")], [],
            Tip: "A spelling change in *yo* keeps the sound: *c* becomes *qu*, *g* becomes *gu* and *z* becomes *c* before *é*: *busqué*, *llegué*, *empecé*."),

        new("pret-ser-ir", 1, "Preterite: ser and ir",
            "*ser* and *ir* share one preterite: *fui, fuiste, fue, fuimos, fueron*. *Fue* is \"he was\" or \"he went\": the sentence tells you which.",
            UnitGoal.Use, UnitKind.Memorise, [], [W(["ser", "ir"], "pret")]),

        new("pret-er-ir", 1, "Preterite: -er and -ir verbs",
            "*-er* and *-ir* verbs share one set: *comí, comiste, comió, comimos, comieron*. Between two vowels the *i* becomes *y*: *leyó, creyeron, oyó*.",
            UnitGoal.Use, UnitKind.Pattern, [S("pret", "er-ir"), S("pret", "y")], [W(["oír"], "pret")]),

        new("pret-u", 1, "Strong preterites with u",
            "A few common verbs swap in a stem with *u* and take endings with no accent: *tener* → *tuve*, *estar* → *estuve*, *poder* → *pude*, *poner* → *puse*, *saber* → *supe*.",
            UnitGoal.Use, UnitKind.Group, [S("pret", "u-stem")], []),

        new("pret-ij", 1, "Strong preterites with i and j",
            "*querer* → *quise*, *hacer* → *hice* (*hizo*), *venir* → *vine*. Stems ending in *j* drop the *i* in *ellos*: *decir* → *dije*, *dijeron*; *traer* → *traje*, *trajeron*.",
            UnitGoal.Use, UnitKind.Group, [S("pret", "i-stem"), S("pret", "j-stem")], []),

        new("impf-reg", 1, "Imperfect",
            "For how things were, what used to happen, or what was going on: *-aba* for *-ar* verbs (*hablaba*), *-ía* for *-er* and *-ir* verbs (*comía*). *yo* and *él* share a form: *hablaba* is I or he was speaking.",
            UnitGoal.Use, UnitKind.Pattern, [S("impf", "ar"), S("impf", "er-ir")], []),

        new("impf-irr", 1, "Imperfect: ser, ir, ver",
            "Only three verbs are irregular in the imperfect: *era* (I was, he was), *iba* (I used to go, I was going), *veía* (I used to see).",
            UnitGoal.Use, UnitKind.Memorise, [], [W(["ser", "ir", "ver"], "impf")]),

        new("pret-impf", 1, "Preterite or imperfect?",
            "The preterite tells what happened: one finished event. The imperfect sets the scene: what was going on, what used to happen, how things were.",
            UnitGoal.Use, UnitKind.Choose, [], [],
            Choose: ["pret", "impf"],
            Idea:
            [
                "*Ayer*, *anoche*, *de repente*, *una vez*, *el año pasado*: usually the preterite.",
                "*Antes*, *de niño*, *siempre*, *todos los días*, *mientras*: usually the imperfect.",
                "Both in one sentence: *Mientras comía, sonó el teléfono*. The scene (imperfect), then what happened (preterite).",
            ]),

        new("pres-dar-ver", 1, "dar, ver, saber, oír",
            "Small irregular verbs: *doy* (I give), *veo* (I see), *sé* (I know), *oigo* (I hear). In the preterite *dar* and *ver* take *-er* endings without accents: *di, dio, vi, vio*.",
            UnitGoal.Use, UnitKind.Memorise, [], [W(["dar", "ver", "saber", "oír"], "pres"), W(["dar", "ver"], "pret")]),

        new("pres-ei-zco", 1, "e → i, and -zco",
            "*pedir* → *pido*, *seguir* → *sigo*, *repetir* → *repito*: the *e* becomes *i* (not in *nosotros*). Verbs ending in *-cer* and *-cir* add a *z* in *yo*: *conozco*, *parezco*, *conduzco*.",
            UnitGoal.Use, UnitKind.Group, [S("pres", "e-i"), S("pres", "zco")], []),

        new("reflexive", 1, "Reflexive verbs",
            "Verbs like *llamarse* and *quedarse* carry a pronoun that matches who: *me llamo*, *te quedas*, *se fue*. The pronoun goes before the verb, or on the end of an infinitive: *voy a quedarme*.",
            UnitGoal.Use, UnitKind.Group, [S("pres", "refl"), S("near", "refl"), S("pret", "refl"), S("impf", "refl")], []),

        // ---------------------------------------------------------------- tier 2: experiences, requests, wishes, later
        new("perf", 2, "Present perfect",
            "*he*, *has*, *ha*, *hemos*, *han* and the participle (*-ado*, *-ido*): *he comido* (I have eaten). A few participles are irregular: *hecho*, *dicho*, *visto*, *puesto*, *vuelto*, *escrito*, *abierto*, *roto*.",
            UnitGoal.Use, UnitKind.Group, [S("perf", "*")], [],
            Tip: "Latin America often uses the preterite where Spain uses this tense: *¿Ya comiste?* (Have you eaten yet?)."),

        new("cmd-usted", 2, "Commands with usted and ustedes",
            "To ask a stranger, a guide or a driver: take the *yo* present, drop the *-o* and swap the vowel (*-ar* verbs take *e*, *-er* and *-ir* verbs take *a*): *hable*, *coma*, *diga*, *pase*. Add *-n* for *ustedes*. *No* goes in front: *no se preocupe*.",
            UnitGoal.Use, UnitKind.Group, [S("cmd", "*", "usted", "ustedes")], []),

        new("cmd-tu", 2, "Commands with tú",
            "For friends, use the *él* present: *habla*, *come*, *escribe*. Eight short ones to learn: *ten, pon, haz, di, ven, sal, ve, sé*.",
            UnitGoal.Use, UnitKind.Group, [S("cmd", "*", "tu", "vos")], []),

        new("subj-reg", 2, "Present subjunctive",
            "After *que* when you want, hope or need something to happen, and after *ojalá*: swap the vowel. *-ar* verbs take *e* (*hable*), *-er* and *-ir* verbs take *a* (*coma*, *viva*). *Quiero que hables*: I want you to speak.",
            UnitGoal.Use, UnitKind.Pattern, [S("subj", "ar"), S("subj", "er-ir")], []),

        new("subj-irr", 2, "Subjunctive from the yo form",
            "Start from the *yo* present and the changes come along: *tengo* → *tenga*, *hago* → *haga*, *conozco* → *conozca*. Six to learn by heart: *sea, vaya, sepa, haya, dé, esté*. The *tú* don't-commands use it too: *no hables*, *no digas*.",
            UnitGoal.Use, UnitKind.Group, [S("subj", "*x"), S("cmd", "*", "tu-neg", "vos-neg", "nos")], []),

        new("subj-or-not", 2, "Subjunctive or not?",
            "The subjunctive is for wishes, doubts, feelings and things that aren't real yet. Facts and habits take the normal present.",
            UnitGoal.Use, UnitKind.Choose, [], [],
            Choose: ["subj", "pres"],
            Idea:
            [
                "*Quiero que*, *espero que*, *ojalá*, *para que*, *no creo que*: subjunctive.",
                "*Creo que*, *sé que*, *normalmente*, *ahora*: the normal present.",
                "*Cuando* about the future takes the subjunctive (*cuando llegues*); about a habit, the present (*cuando llego*).",
            ]),

        new("fut", 2, "Future",
            "Add *-é, -ás, -á, -emos, -án* to the whole infinitive: *hablaré* (I will speak). Some verbs shorten it first: *tendré, podré, haré, diré, saldré, vendré*. In conversation *voy a* and an infinitive is more common, and the future often means a guess: *¿Dónde estará?* (Where could it be?).",
            UnitGoal.ReadFirst, UnitKind.Group, [S("fut", "*")], []),

        new("pret-stem-ir", 2, "Preterite: pidió, durmió",
            "*-ir* verbs that change their stem in the present change again in *él* and *ellos* in the preterite: *pedir* → *pidió, pidieron*; *dormir* → *durmió, durmieron*.",
            UnitGoal.Use, UnitKind.Group, [S("pret", "e-i"), S("pret", "o-u")], []),

        // ---------------------------------------------------------------- tier 3: would, if, the past of the past
        new("cond", 3, "Conditional",
            "Would: add *-ía* to the whole infinitive, *hablaría* (I would speak), with the same short stems as the future: *tendría, podría, haría*. *¿Podría ayudarme?* is the polite way to ask.",
            UnitGoal.Use, UnitKind.Group, [S("cond", "*")], []),

        new("impsubj", 3, "Imperfect subjunctive",
            "Take the *ellos* preterite, drop *-ron* and add *-ra*: *hablaron* → *hablara*, *tuvieron* → *tuviera*. It comes after *si* for things that aren't real (*si tuviera tiempo…*) and after past wishes (*quería que vinieras*). *Quisiera* (I'd like) is one you know already.",
            UnitGoal.ReadFirst, UnitKind.Group, [S("impsubj", "*")], []),

        new("plup", 3, "Pluperfect",
            "*había* and the participle: *había comido* (I had eaten). For something that happened before another moment in the past.",
            UnitGoal.Use, UnitKind.Group, [S("plup", "*")], []),

        new("rare", 3, "Less common changes",
            "A few small groups: *jugar* → *juego*, *proteger* → *protejo*, *construir* → *construyo*, *enviar* → *envío*, *continuar* → *continúo*, and *reír* → *río*.",
            UnitGoal.Use, UnitKind.Group,
            [S("pres", "u-ue"), S("pres", "g-j"), S("pres", "uir-y"), S("pres", "accent"), S("pres", "*x"), S("pret", "*x"), S("impf", "*x")], []),

        // ---------------------------------------------------------------- tier 4: read only
        new("read-only", 4, "Rare compound tenses",
            "Four tenses built from *haber* that you'll mostly read: *habré hecho* (I will have done), *habría hecho* (I would have done), *haya hecho* (that I have done), *hubiera hecho* (if I had done). The first word says who and when; the participle says which verb. With vosotros off in Settings, its forms show up here too, just to recognise: *habláis*, *coméis*, *fuisteis*.",
            UnitGoal.ReadOnly, UnitKind.Group, [S("futperf", "*"), S("condperf", "*"), S("subjperf", "*"), S("plupsubj", "*")], [],
            VosotrosRead: true),
    ];
}
