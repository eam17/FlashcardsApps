"""Clue-word sentences for the "Clue words" game. Run from the repo root:
    python tools/verb-games/clues_src.py
It checks every sentence against verbs.json (the verb has the form, the wrong options differ) and writes
SpanishFlashcards/Models/Verbs/VerbClues.Data.cs.
Fields: Spanish with ___, verb, person (0 yo, 1 tú, 2 él, 3 nosotros, 4 vosotros, 5 ellos), tense,
clue words (shown highlighted), English, why.
"""
import json
CLUES = [
 ("Ayer ___ paella con mis amigos.", "comer", 0, "pret", "Ayer", "Yesterday I ate paella with my friends.", "*Ayer* and one finished action: preterite."),
 ("Anoche Ana ___ a las once.", "llegar", 2, "pret", "Anoche", "Last night Ana arrived at eleven.", "*Anoche* and a moment in time: preterite."),
 ("El año pasado mis padres ___ a México.", "viajar", 5, "pret", "El año pasado", "Last year my parents travelled to Mexico.", "*El año pasado*: a finished time, so preterite."),
 ("De repente, ella ___ la puerta.", "abrir", 2, "pret", "De repente", "Suddenly, she opened the door.", "*De repente* (suddenly): something happened, preterite."),
 ("Una vez ___ una película de miedo y no dormí.", "ver", 0, "pret", "Una vez", "Once I saw a horror film and didn't sleep.", "*Una vez* (once): one event, preterite."),
 ("La semana pasada mis padres me ___ un regalo.", "dar", 5, "pret", "La semana pasada", "Last week my parents gave me a present.", "*La semana pasada*: finished time, preterite."),
 ("¿Qué ___ tú el sábado pasado?", "hacer", 1, "pret", "el sábado pasado", "What did you do last Saturday?", "*El sábado pasado*: a finished day, preterite."),
 ("Ayer no ___ tiempo para nada.", "tener", 0, "pret", "Ayer", "Yesterday I didn't have time for anything.", "*Ayer*, seen as a whole and finished: preterite."),
 ("Hace dos días ___ a tu hermano en el mercado.", "ver", 3, "pret", "Hace dos días", "Two days ago we saw your brother at the market.", "*Hace dos días* (two days ago): preterite."),
 ("De niño, ___ al fútbol todos los días.", "jugar", 0, "impf", "De niño", "As a child, I played football every day.", "*De niño* and a habit: imperfect (used to)."),
 ("Cuando era pequeña, mi abuela siempre ___ sopa los domingos.", "cocinar", 2, "impf", "siempre", "When I was little, my grandmother always cooked soup on Sundays.", "*Siempre* in the past: a habit, imperfect."),
 ("Antes mi familia ___ en un pueblo muy pequeño.", "vivir", 2, "impf", "Antes", "My family used to live in a very small village.", "*Antes* (before, back then): imperfect."),
 ("Mientras yo ___, mi hermano cocinaba.", "leer", 0, "impf", "Mientras", "While I was reading, my brother was cooking.", "*Mientras* (while): something going on, imperfect."),
 ("Todos los veranos ___ a la playa con mis primos.", "ir", 0, "impf", "Todos los veranos", "Every summer I went to the beach with my cousins.", "*Todos los veranos*: a repeated past habit, imperfect."),
 ("A menudo mis amigos ___ por el parque.", "pasear", 5, "impf", "A menudo", "My friends often used to walk in the park.", "*A menudo* (often) in the past: imperfect."),
 ("Cuando era niña, ___ miedo de los perros.", "tener", 0, "impf", "Cuando era niña", "When I was a girl, I was afraid of dogs.", "*Cuando era niña*: how things were, imperfect."),
 ("Normalmente ___ café por la mañana.", "tomar", 0, "pres", "Normalmente", "I normally drink coffee in the morning.", "*Normalmente*: what you usually do now, present."),
 ("Ahora mismo mi hermana ___ la televisión.", "mirar", 2, "pres", "Ahora mismo", "Right now my sister is watching TV.", "*Ahora mismo* (right now): present."),
 ("Cada día ___ un poco de español.", "estudiar", 0, "pres", "Cada día", "Every day I study a little Spanish.", "*Cada día* about now: present."),
 ("¿Dónde ___ tú ahora?", "vivir", 1, "pres", "ahora", "Where do you live now?", "*Ahora*: present."),
 ("Hoy en día, mucha gente ___ en casa.", "trabajar", 2, "pres", "Hoy en día", "Nowadays, lots of people work at home.", "*Hoy en día* (nowadays): present."),
 ("Mañana ___ a mi madre.", "llamar", 0, "fut", "Mañana", "Tomorrow I'll call my mother.", "*Mañana*: future. (The present, *llamo*, is also heard for plans.)"),
 ("La semana que viene ___ a Madrid.", "ir", 3, "fut", "La semana que viene", "Next week we'll go to Madrid.", "*La semana que viene* (next week): future."),
 ("Algún día ___ un libro.", "escribir", 1, "fut", "Algún día", "Some day you'll write a book.", "*Algún día* (some day): future."),
 ("El año que viene mis hijos ___ diez años.", "tener", 5, "fut", "El año que viene", "Next year my children will be ten.", "*El año que viene*: future."),
 ("Dentro de dos horas ___ la cena.", "hacer", 0, "fut", "Dentro de dos horas", "In two hours I'll make dinner.", "*Dentro de* (in, from now): future."),
 ("Si tuviera dinero, ___ una casa en la playa.", "comprar", 0, "cond", "Si tuviera", "If I had money, I would buy a house on the beach.", "*Si tuviera…* (if I had): the result is conditional, would."),
 ("Yo que tú, no lo ___.", "hacer", 0, "cond", "Yo que tú", "If I were you, I wouldn't do it.", "*Yo que tú* (if I were you): conditional."),
 ("Con más tiempo, nosotros ___ más.", "viajar", 3, "cond", "Con más tiempo", "With more time, we would travel more.", "Something imagined (*con más tiempo*): conditional."),
 ("Quiero que tú ___ conmigo.", "venir", 1, "subj", "Quiero que", "I want you to come with me.", "*Quiero que* + someone else: subjunctive."),
 ("Ojalá ___ sol mañana.", "hacer", 2, "subj", "Ojalá", "I hope it's sunny tomorrow.", "*Ojalá* (I hope): subjunctive."),
 ("Espero que ustedes ___ bien.", "estar", 5, "subj", "Espero que", "I hope you're all well.", "*Espero que* (I hope that): subjunctive."),
 ("Cuando ___ a casa, llámame.", "llegar", 1, "subj", "Cuando", "When you get home, call me.", "*Cuando* about the future: subjunctive."),
 ("No creo que ella ___ la verdad.", "saber", 2, "subj", "No creo que", "I don't think she knows the truth.", "*No creo que* (I don't think): subjunctive."),
 ("Te lo explico para que lo ___.", "entender", 1, "subj", "para que", "I'll explain it so that you understand it.", "*Para que* (so that): always subjunctive."),
 ("Si yo ___ más tiempo, aprendería a bailar.", "tener", 0, "impsubj", "Si", "If I had more time, I would learn to dance.", "*Si* + something not real, with *aprendería*: imperfect subjunctive."),
 ("Quería que tú ___ a la fiesta.", "venir", 1, "impsubj", "Quería que", "I wanted you to come to the party.", "*Quería que* (past wish): imperfect subjunctive."),
 ("Me pidió que ___ la puerta.", "cerrar", 0, "impsubj", "Me pidió que", "She asked me to close the door.", "*Me pidió que* (past request): imperfect subjunctive."),
 ("Habla como si ___ el jefe.", "ser", 2, "impsubj", "como si", "He talks as if he were the boss.", "*Como si* (as if): always imperfect subjunctive."),
 ("Cuando llegué, la película ya ___.", "empezar", 2, "plup", "Cuando llegué, ya", "When I arrived, the film had already started.", "Before another past moment (*cuando llegué… ya*): pluperfect, had done."),
 ("Antes de ese viaje, nunca ___ el mar.", "ver", 0, "plup", "Antes de ese viaje, nunca", "Before that trip, I had never seen the sea.", "Earlier than a past moment: pluperfect."),
]
RIVALS = {
 'pres':['pret','impf','fut'], 'pret':['impf','pres','fut'], 'impf':['pret','pres','cond'], 'fut':['cond','pret','subj'],
 'cond':['fut','impf','pres'], 'subj':['pres','fut','impsubj'], 'impsubj':['subj','impf','cond'], 'plup':['perf','pres','fut'],
}
verbs={v['inf']:v for v in json.load(open('SpanishFlashcards/wwwroot/data/verbs.json',encoding='utf-8'))['verbs']}
HAB={'pres':['he','has','ha','hemos','habéis','han'],'impf':['había','habías','había','habíamos','habíais','habían']}
def form(inf,t,p):
    v=verbs[inf]
    if t in ('perf','plup'):
        return (HAB['pres' if t=='perf' else 'impf'][p])+' '+v['pp']
    f=v['f'].get(t)
    return f[p].split('|')[0] if f and f[p] else None
bad=0
for es,inf,p,t,clue,en,why in CLUES:
    assert '___' in es, es
    assert clue.split(',')[0].lower() in es.lower(), (clue, es)
    right=form(inf,t,p); opts=[form(inf,r,p) for r in RIVALS[t]]
    if right is None or None in opts or right in opts or len(set(opts))<3:
        print('BAD',es,right,opts); bad+=1
    print(f'{t:8} {es.replace("___","["+right+"]"):70} | {", ".join(opts)}')
print('bad',bad,'total',len(CLUES))
def cs(s): return '"'+s.replace('\\','\\\\').replace('"','\\"')+'"'
lines=['// Generated by tools/verb-games/clues_src.py. Edit the sentences there and run it again.','namespace SpanishFlashcards.Models.Verbs;','','public static partial class VerbClues','{','    public static readonly IReadOnlyList<ClueSentence> Sentences =','    [']
for es,inf,p,t,clue,en,why in CLUES:
    lines.append(f'        new({cs(es)}, {cs(inf)}, {p}, {cs(t)}, {cs(clue)}, {cs(en)}, {cs(why)}),')
lines+=['    ];','','    /// <summary>The wrong options for each tense: the tenses it gets mixed up with.</summary>','    public static readonly IReadOnlyDictionary<string, string[]> Rivals = new Dictionary<string, string[]>','    {']
for t,r in RIVALS.items(): lines.append(f'        [{cs(t)}] = [{", ".join(cs(x) for x in r)}],')
lines+=['    };','}','']
open('SpanishFlashcards/Models/Verbs/VerbClues.Data.cs','w',encoding='utf-8').write('\n'.join(lines))
