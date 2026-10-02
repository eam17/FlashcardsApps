"""Builds Models/Verbs/VerbEnglish.Data.cs: English forms for each verb's main meaning (base, he-form,
past, past participle), used by the "What does it mean?" game. Run from the repo root:
    python tools/verb-english/gen_english.py
Irregular English verbs are listed below; anything else follows the regular -ed rules. Overrides set the
meaning used (the first one in the word list, made to fit "I ___").
"""
import json, re, os

IRREG = {  # base: (past, past participle)
 'be':('was','been'),'have':('had','had'),'do':('did','done'),'make':('made','made'),'say':('said','said'),'tell':('told','told'),
 'go':('went','gone'),'see':('saw','seen'),'give':('gave','given'),'know':('knew','known'),'put':('put','put'),'think':('thought','thought'),
 'speak':('spoke','spoken'),'leave':('left','left'),'find':('found','found'),'come':('came','come'),'take':('took','taken'),
 'feel':('felt','felt'),'begin':('began','begun'),'write':('wrote','written'),'lose':('lost','lost'),'understand':('understood','understood'),
 'get':('got','gotten'),'keep':('kept','kept'),'read':('read','read'),'fall':('fell','fallen'),'hear':('heard','heard'),'win':('won','won'),
 'bring':('brought','brought'),'eat':('ate','eaten'),'drink':('drank','drunk'),'sleep':('slept','slept'),'run':('ran','run'),
 'buy':('bought','bought'),'sell':('sold','sold'),'pay':('paid','paid'),'cost':('cost','cost'),'spend':('spent','spent'),
 'teach':('taught','taught'),'sing':('sang','sung'),'forget':('forgot','forgotten'),'choose':('chose','chosen'),'sit':('sat','sat'),
 'wake':('woke','woken'),'break':('broke','broken'),'build':('built','built'),'cut':('cut','cut'),'send':('sent','sent'),
 'show':('showed','shown'),'mean':('meant','meant'),'dream':('dreamed','dreamed'),'hurt':('hurt','hurt'),'grow':('grew','grown'),
 'fight':('fought','fought'),'throw':('threw','thrown'),'catch':('caught','caught'),'drive':('drove','driven'),'fly':('flew','flown'),
 'swim':('swam','swum'),'draw':('drew','drawn'),'sew':('sewed','sewn'),'lead':('led','led'),'hold':('held','held'),'let':('let','let'),
 'wear':('wore','worn'),'knit':('knitted','knitted'),'stand':('stood','stood'),'set':('set','set'),'shut':('shut','shut'),'meet':('met','met'),'lie':('lay','lain'),
}
OVERRIDE = {  # Spanish infinitive: English base phrase
 'ser':'be','estar':'be','haber':None,'poder':'be able to','deber':'have to','saber':'know','conocer':'know',
 'querer':'want','pasar':'happen','quedar':'stay','creer':'believe','llevar':'carry','dejar':'leave','seguir':'follow',
 'salir':'go out','tomar':'take','tratar':'try','mirar':'look at','contar':'count','esperar':'wait','buscar':'look for',
 'perder':'lose','pedir':'ask for','servir':'serve','sacar':'take out','mantener':'keep','resultar':'turn out',
 'presentar':'introduce','acabar':'finish','convertir':'turn into','ganar':'win','partir':'leave','realizar':'carry out',
 'andar':'walk','subir':'go up','bajar':'go down','enseñar':'teach','gustar':'like','encantar':'love','desear':'wish',
 'acordarse':'remember','levantar':'lift','sentar':'sit','vestir':'dress','duchar':'shower','tocar':'touch',
 'guardar':'keep','mandar':'send','cuidar':'take care of','faltar':'be missing','sobrar':'be left over','funcionar':'work',
 'tirar':'throw','coger':'catch','cumplir':'fulfill','divertir':'amuse','casar':'marry','manejar':'drive','mudarse':'move house',
 'llamarse':'be called','sentirse':'feel','irse':'leave','quedarse':'stay','pasear':'take a walk','probar':'try',
 'llover':'rain','nevar':'snow','nacer':'be born','despertar':'wake up','girar':'turn','apagar':'turn off','encender':'turn on',
 'importar':'matter','preocupar':'worry','molestar':'bother','doler':'hurt','parecer':'seem','ocurrir':'happen',
 'significar':'mean','comenzar':'begin','empezar':'begin','volver':'come back','entrar':'go in','existir':'exist',
}
def ing_ok(): pass
VOW='aeiou'
def regular_past(b):
    if b.endswith('e'): return b+'d'
    if b.endswith('y') and len(b)>1 and b[-2] not in VOW: return b[:-1]+'ied'
    # double final consonant: short one-syllable CVC (stop -> stopped), plus a few two-syllable ones
    if b in ('stop','plan','shop','drop','travel','chat','prefer','control','admit','permit','occur','rob','beg','grab','hug','nod','rub','tap','trip','fit','ship','step'):
        if b=='travel': return 'traveled'
        return b+b[-1]+'ed'
    return b+'ed'
def s_form(b):
    if b in ('be',): return 'is'
    if b=='have': return 'has'
    if b=='do': return 'does'
    if b=='go': return 'goes'
    if re.search(r'(s|sh|ch|x|z|o)$',b): return b+'es'
    if b.endswith('y') and b[-2] not in VOW: return b[:-1]+'ies'
    return b+'s'

verbs=json.load(open('SpanishFlashcards/wwwroot/data/verbs.json',encoding='utf-8'))['verbs']
out={}
for v in verbs:
    inf=v['inf']
    if inf in OVERRIDE:
        phrase=OVERRIDE[inf]
        if phrase is None: continue
    else:
        first=re.split(r'[,;]',v['en'])[0]
        first=re.sub(r'\(.*?\)','',first).strip()
        phrase=first[3:] if first.startswith('to ') else first
    words=phrase.split(' ')
    head,rest=words[0],' '.join(words[1:])
    past,pp=IRREG.get(head,(None,None))
    if past is None: past=pp=regular_past(head)
    tail=(' '+rest) if rest else ''
    out[inf]=[phrase, s_form(head)+tail, past+tail, pp+tail]
print(len(out))
for k,(b,s,p,pp) in out.items(): print(f'{k:12} {b:18} {s:20} {p:18} {pp}')
lines=['// Generated by tools/verb-english/gen_english.py. Do not edit by hand.','namespace SpanishFlashcards.Models.Verbs;','','public static partial class VerbEnglish','{','    /// <summary>Infinitive → English (base, he/she form, past, past participle) for its main meaning.</summary>','    private static readonly Dictionary<string, string[]> Data = new()','    {']
for k,arr in out.items():
    lines.append('        ["%s"] = [%s],' % (k, ', '.join('"%s"'%a for a in arr)))
lines += ['    };','}','']
open('SpanishFlashcards/Models/Verbs/VerbEnglish.Data.cs','w',encoding='utf-8').write('\n'.join(lines))
