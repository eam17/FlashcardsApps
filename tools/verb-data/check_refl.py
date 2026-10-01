import json, sys
EXPECT = {  # tú, no tú, usted, nosotros, vosotros, no vosotros, ustedes (standard forms, RAE)
 "acordarse": "acuérdate|te acuerdes|acuérdese|acordémonos|acordaos|os acordéis|acuérdense",
 "mudarse":   "múdate|te mudes|múdese|mudémonos|mudaos|os mudéis|múdense",
 "llamarse":  "llámate|te llames|llámese|llamémonos|llamaos|os llaméis|llámense",
 "sentirse":  "siéntete|te sientas|siéntase|sintámonos|sentíos|os sintáis|siéntanse",
 "irse":      "vete|te vayas|váyase|vámonos|idos|os vayáis|váyanse",
 "quedarse":  "quédate|te quedes|quédese|quedémonos|quedaos|os quedéis|quédense",
}
v = {x["inf"]: x for x in json.load(open(sys.argv[1], encoding="utf-8"))["verbs"]}
bad = 0
for inf, exp in EXPECT.items():
    for o, e in zip(v[inf]["f"]["cmd"], exp.split("|")):
        if e not in o.split("|"): bad += 1; print(inf, o, "expected", e)
print(f"reflexive commands: {len(EXPECT)*7} forms, {bad} disagreements")
