"""Builds SpanishFlashcards/wwwroot/data/dict.tsv, the offline Spanish-English dictionary used by the Read tab.

Source: github.com/doozan/spanish_data (clone it next to this script, or pass its path):
  es-en.data     Spanish-English glosses from Wiktionary (CC BY-SA)
  frequency.csv  lemmas by frequency with the word forms seen for each (CC BY-SA 3.0, hermitdave/FrequencyWords)
The output is therefore CC BY-SA too (see wwwroot/data/dict-LICENSE.txt).

    git clone --depth 1 https://github.com/doozan/spanish_data.git /tmp/spanish_data
    python tools/dictionary/build_dict.py /tmp/spanish_data

One line per lemma, most frequent first:  lemma <TAB> pos <TAB> gender <TAB> meaning <TAB> forms (comma separated)
"""
import csv, re, sys, os

src = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), 'spanish_data')
MAX_LEMMAS = 20000
KEEP_POS = {'n', 'v', 'adj', 'adv', 'prep', 'conj', 'pron', 'interj', 'num', 'determiner', 'art', 'contraction', 'phrase', 'particle'}

# ---- es-en.data: lemma -> pos -> (gender, [glosses])
entries = {}
word = pos = None
SKIP = re.compile(r'^(?:\(.*?\)\s*)?(?:plural|feminine|masculine|inflection|alternative|obsolete|archaic|misspelling|pronunciation|apocopic|'
                  r'eye dialect|nonstandard|informal spelling|form|clipping|abbreviation|initialism|acronym|contraction|synonym|'
                  r'superseded|rare spelling|dated spelling|euphemistic spelling)\b.*\bof\b', re.I)
with open(os.path.join(src, 'es-en.data'), encoding='utf-8') as f:
    for line in f:
        line = line.rstrip('\n')
        if line == '_____':
            word = pos = None
            continue
        if word is None:
            word = line
            continue
        if line.startswith('pos: '):
            pos = line[5:]
            entries.setdefault(word, {}).setdefault(pos, ['', []])
            continue
        if pos is None:
            continue
        if line.startswith('  g: '):
            if not entries[word][pos][0]:
                entries[word][pos][0] = line[5:].strip()
        elif line.startswith('  gloss: '):
            entries[word][pos][1].append(line[9:].strip())

def clean(g):
    g = re.sub(r'\s*\([^()]*\)', '', g)          # drop (qualifiers)
    g = re.sub(r'\s*\[[^\[\]]*\]', '', g)        # and [notes]
    g = re.sub(r'"([^"]*)"', r'\1', g)
    g = re.sub(r'\s+', ' ', g).strip(' ;,.')
    return g

def meaning(lemma, pos):
    e = entries.get(lemma, {})
    order = [pos] + [p for p in e if p != pos]
    for p in order:
        if p not in e:
            continue
        gender, glosses = e[p]
        good = []
        for g in glosses:
            if SKIP.search(g):
                continue
            for part in clean(g).split(';'):
                part = part.strip(' ,.')
                if not part or part.lower() in (x.lower() for x in good) or len(part) > 60:
                    continue
                good.append(part)
            if len(good) >= 3:
                break
        good = good[:3]
        if good:
            text = '; '.join(good)
            if len(text) > 70:
                text = text[:67].rsplit(' ', 1)[0] + '…'
            return p, gender, text
    return None

out = []
seen = set()
with open(os.path.join(src, 'frequency.csv'), encoding='utf-8') as f:
    r = csv.reader(f)
    next(r)
    for row in r:
        count, lemma, pos, flags, usage = row[:5]
        if pos not in KEEP_POS or 'NOUSAGE' in flags or 'DUPLICATE' in flags:
            continue
        if lemma in seen:
            continue
        m = meaning(lemma, pos)
        if m is None:
            continue
        p, gender, text = m
        forms = []
        for u in usage.split('|'):
            if ':' not in u:
                continue
            n, form = u.split(':', 1)
            form = form.strip().lower()
            if form and form != lemma.lower() and form not in forms and int(n) >= 3 and re.fullmatch(r"[a-záéíóúüñ]+", form):
                forms.append(form)
        seen.add(lemma)
        g = gender if gender in ('m', 'f', 'mf', 'm-p', 'f-p') else ''
        out.append('\t'.join([lemma, p, g, text.replace('\t', ' '), ','.join(forms)]))
        if len(out) >= MAX_LEMMAS:
            break

dst = os.path.join(os.path.dirname(__file__), '..', '..', 'SpanishFlashcards', 'wwwroot', 'data', 'dict.tsv')
with open(dst, 'w', encoding='utf-8', newline='\n') as f:
    f.write('\n'.join(out) + '\n')
print(len(out), 'lemmas,', os.path.getsize(dst), 'bytes')
