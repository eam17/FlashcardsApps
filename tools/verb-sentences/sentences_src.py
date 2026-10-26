"""Sentences with a gap for the Verbs tab's units: making a form in context (no tense label, just the sentence,
its English and the infinitive) and reading a form in a sentence. Run from the repo root:
    python tools/verb-sentences/sentences_src.py
It checks every sentence against verbs.json (the verb exists and has the form, the gap appears once, the clue is in
the sentence, the tense belongs to the unit) and writes SpanishFlashcards/Models/Verbs/VerbSentences.Data.cs.

Fields: Spanish with ___ where the verb goes, verb, person, tense, English, unit id, clue words (optional, shown
highlighted), other accepted answers (optional, separated by |).
Persons: 0 yo, 1 tú, 2 él / usted, 3 nosotros, 4 vosotros, 5 ellos / ustedes.
Commands (cmd): 0 tú, 1 tú negative, 2 usted, 3 nosotros, 4 vosotros, 5 vosotros negative, 6 ustedes.
The gap holds the whole form: "se llama", "vamos a comer", "he visto". After "No" in a negative command, the gap is
the verb alone.
Set in Peru where it fits: the app is set up for travelling there.
"""
import json
import sys

S = [
    # ---------------------------------------------------------------- present -ar
    ("En Lima ___ mucho español.", "hablar", 3, "pres", "In Lima we speak a lot of Spanish.", "pres-ar"),
    ("¿Usted ___ en el centro?", "trabajar", 2, "pres", "Do you work in the centre?", "pres-ar"),
    ("Mis amigos ___ el tren a Cusco.", "tomar", 5, "pres", "My friends take the train to Cusco.", "pres-ar"),
    ("¿Tú ___ mucho por la ciudad?", "caminar", 1, "pres", "Do you walk a lot around the city?", "pres-ar"),
    ("Ella ___ fruta en el mercado los sábados.", "comprar", 2, "pres", "She buys fruit at the market on Saturdays.", "pres-ar"),
    ("Ustedes ___ muy bien.", "bailar", 5, "pres", "You all dance very well.", "pres-ar"),
    ("Siempre ___ a mi madre los domingos.", "llamar", 0, "pres", "I always call my mother on Sundays.", "pres-ar"),

    # ---------------------------------------------------------------- ser and estar
    ("Yo ___ de Estados Unidos.", "ser", 0, "pres", "I'm from the United States.", "ser-estar"),
    ("¿Dónde ___ el baño, por favor?", "estar", 2, "pres", "Where is the bathroom, please?", "ser-estar"),
    ("Nosotros ___ cansados por la altura.", "estar", 3, "pres", "We're tired from the altitude.", "ser-estar"),
    ("Cusco ___ una ciudad muy bonita.", "ser", 2, "pres", "Cusco is a very beautiful city.", "ser-estar"),
    ("¿Ustedes ___ de aquí?", "ser", 5, "pres", "Are you all from here?", "ser-estar"),
    ("¿Cómo ___ usted?", "estar", 2, "pres", "How are you?", "ser-estar"),
    ("Tú ___ muy amable.", "ser", 1, "pres", "You're very kind.", "ser-estar"),
    ("Las ruinas ___ cerca del pueblo.", "estar", 5, "pres", "The ruins are near the town.", "ser-estar"),

    # ---------------------------------------------------------------- present -er and -ir
    ("En Perú ___ mucho arroz.", "comer", 3, "pres", "In Peru we eat a lot of rice.", "pres-er-ir"),
    ("¿Usted ___ en Lima?", "vivir", 2, "pres", "Do you live in Lima?", "pres-er-ir"),
    ("Yo ___ mucho café.", "beber", 0, "pres", "I drink a lot of coffee.", "pres-er-ir"),
    ("Mis padres ___ en un pueblo pequeño.", "vivir", 5, "pres", "My parents live in a small town.", "pres-er-ir"),
    ("¿Tú ___ el periódico?", "leer", 1, "pres", "Do you read the newspaper?", "pres-er-ir"),
    ("La tienda ___ a las nueve.", "abrir", 2, "pres", "The shop opens at nine.", "pres-er-ir"),
    ("Nosotros ___ postales a la familia.", "escribir", 3, "pres", "We write postcards to the family.", "pres-er-ir"),
    ("¿Ustedes ___ pescado?", "comer", 5, "pres", "Do you all eat fish?", "pres-er-ir"),

    # ---------------------------------------------------------------- ir, and going to
    ("Mañana ___ a Machu Picchu.", "ir", 0, "pres", "Tomorrow I'm going to Machu Picchu.", "ir-going"),
    ("¿A dónde ___ usted?", "ir", 2, "pres", "Where are you going?", "ir-going"),
    ("Mis amigos ___ al mercado.", "ir", 5, "pres", "My friends are going to the market.", "ir-going"),
    ("Esta noche ___ ceviche.", "comer", 3, "near", "Tonight we're going to eat ceviche.", "ir-going"),
    ("¿Qué ___ tú el sábado?", "hacer", 1, "near", "What are you going to do on Saturday?", "ir-going"),
    ("El tren ___ a las seis.", "salir", 2, "near", "The train is going to leave at six.", "ir-going"),
    ("Ustedes ___ un abrigo.", "necesitar", 5, "near", "You're going to need a coat.", "ir-going"),

    # ---------------------------------------------------------------- -go verbs
    ("Yo ___ dos hermanos.", "tener", 0, "pres", "I have two brothers.", "pres-go"),
    ("¿Usted ___ habitaciones libres?", "tener", 2, "pres", "Do you have any rooms free?", "pres-go"),
    ("¿Qué ___ tú los domingos?", "hacer", 1, "pres", "What do you do on Sundays?", "pres-go"),
    ("Siempre ___ la verdad.", "decir", 0, "pres", "I always tell the truth.", "pres-go"),
    ("El bus ___ a las ocho.", "salir", 2, "pres", "The bus leaves at eight.", "pres-go"),
    ("¿___ tú a la fiesta?", "venir", 1, "pres", "Are you coming to the party?", "pres-go"),
    ("Yo ___ la mochila aquí.", "poner", 0, "pres", "I put my backpack here.", "pres-go"),
    ("Nosotros ___ hambre.", "tener", 3, "pres", "We're hungry.", "pres-go"),

    # ---------------------------------------------------------------- e → ie, o → ue
    ("¿___ pagar con tarjeta?", "poder", 0, "pres", "Can I pay by card?", "pres-stem"),
    ("Yo ___ un café, por favor.", "querer", 0, "pres", "I want a coffee, please.", "pres-stem"),
    ("¿Cuánto ___ el boleto?", "costar", 2, "pres", "How much does the ticket cost?", "pres-stem"),
    ("Nosotros ___ a casa el lunes.", "volver", 3, "pres", "We go back home on Monday.", "pres-stem"),
    ("¿Qué ___ tú de Lima?", "pensar", 1, "pres", "What do you think of Lima?", "pres-stem"),
    ("Los niños ___ mucho en el bus.", "dormir", 5, "pres", "The children sleep a lot on the bus.", "pres-stem"),
    ("La clase ___ a las diez.", "empezar", 2, "pres", "The class starts at ten.", "pres-stem"),
    ("Yo no ___ bien cuando hablas rápido.", "entender", 0, "pres", "I don't understand well when you speak fast.", "pres-stem"),

    # ---------------------------------------------------------------- polite requests (set phrases)
    ("___ un café con leche, por favor.", "querer", 0, "impsubj", "I'd like a coffee with milk, please.", "polite", None, "me gustaría"),
    ("___ una habitación para dos noches.", "querer", 0, "impsubj", "I'd like a room for two nights.", "polite", None, "me gustaría"),
    ("¿___ ayudarme, por favor?", "poder", 2, "cond", "Could you help me, please?", "polite"),
    ("¿Me ___ decir dónde está la estación?", "poder", 2, "cond", "Could you tell me where the station is?", "polite"),
    ("Me ___ ver el menú.", "gustar", 2, "cond", "I'd like to see the menu.", "polite"),
    ("¿___ posible cambiar la fecha?", "ser", 2, "cond", "Would it be possible to change the date?", "polite"),
    ("___ pagar con tarjeta, si se puede.", "querer", 0, "impsubj", "I'd like to pay by card, if I can.", "polite", None, "me gustaría"),
    ("¿Nos ___ traer la cuenta?", "poder", 2, "cond", "Could you bring us the bill?", "polite"),

    # ---------------------------------------------------------------- preterite -ar
    ("Ayer ___ mucho por el centro.", "caminar", 0, "pret", "Yesterday I walked a lot around the centre.", "pret-ar"),
    ("¿Usted ___ el tren de las siete?", "tomar", 2, "pret", "Did you take the seven o'clock train?", "pret-ar"),
    ("Anoche ___ a Cusco muy tarde.", "llegar", 0, "pret", "Last night I got to Cusco very late.", "pret-ar"),
    ("¿Cuánto ___ tú por la alpaca de peluche?", "pagar", 1, "pret", "How much did you pay for the toy alpaca?", "pret-ar"),
    ("Mis amigos ___ toda la noche.", "bailar", 5, "pret", "My friends danced all night.", "pret-ar"),
    ("Ayer ___ las llaves por todas partes.", "buscar", 0, "pret", "Yesterday I looked for the keys everywhere.", "pret-ar"),
    ("El concierto ___ a las nueve.", "empezar", 2, "pret", "The concert started at nine.", "pret-ar"),
    ("Ella me ___ el lunes.", "llamar", 2, "pret", "She called me on Monday.", "pret-ar"),

    # ---------------------------------------------------------------- preterite of ser and ir
    ("El año pasado ___ a Arequipa.", "ir", 0, "pret", "Last year I went to Arequipa.", "pret-ser-ir"),
    ("La fiesta ___ increíble.", "ser", 2, "pret", "The party was amazing.", "pret-ser-ir"),
    ("¿___ ustedes al museo ayer?", "ir", 5, "pret", "Did you all go to the museum yesterday?", "pret-ser-ir"),
    ("¿Cómo ___ el viaje?", "ser", 2, "pret", "How was the trip?", "pret-ser-ir"),
    ("Nosotros ___ en bus a Puno.", "ir", 3, "pret", "We went to Puno by bus.", "pret-ser-ir"),
    ("¿Tú ___ a la playa el domingo?", "ir", 1, "pret", "Did you go to the beach on Sunday?", "pret-ser-ir"),

    # ---------------------------------------------------------------- preterite -er and -ir
    ("Anoche ___ ceviche por primera vez.", "comer", 0, "pret", "Last night I ate ceviche for the first time.", "pret-er-ir"),
    ("¿___ usted mi mensaje?", "recibir", 2, "pret", "Did you get my message?", "pret-er-ir"),
    ("Mis padres ___ en Lima diez años.", "vivir", 5, "pret", "My parents lived in Lima for ten years.", "pret-er-ir"),
    ("¿Tú ___ el libro que te di?", "leer", 1, "pret", "Did you read the book I gave you?", "pret-er-ir"),
    ("Ella ___ la puerta y salió.", "abrir", 2, "pret", "She opened the door and went out.", "pret-er-ir"),
    ("¿___ ustedes la música?", "oír", 5, "pret", "Did you all hear the music?", "pret-er-ir"),
    ("Él no me ___.", "creer", 2, "pret", "He didn't believe me.", "pret-er-ir"),
    ("Ayer ___ una carta a mi abuela.", "escribir", 0, "pret", "Yesterday I wrote a letter to my grandmother.", "pret-er-ir"),

    # ---------------------------------------------------------------- strong preterites with u
    ("Ayer no ___ tiempo para comer.", "tener", 0, "pret", "Yesterday I didn't have time to eat.", "pret-u"),
    ("¿Dónde ___ ustedes anoche?", "estar", 5, "pret", "Where were you all last night?", "pret-u"),
    ("No ___ dormir por el ruido.", "poder", 0, "pret", "I couldn't sleep because of the noise.", "pret-u"),
    ("Ella ___ la mesa para la cena.", "poner", 2, "pret", "She set the table for dinner.", "pret-u"),
    ("¿Cuándo ___ tú la noticia?", "saber", 1, "pret", "When did you find out the news?", "pret-u"),
    ("Nosotros ___ tres días en Cusco.", "estar", 3, "pret", "We were in Cusco for three days.", "pret-u"),

    # ---------------------------------------------------------------- strong preterites with i and j
    ("¿Qué ___ tú el fin de semana?", "hacer", 1, "pret", "What did you do at the weekend?", "pret-ij"),
    ("Mis primos ___ a visitarnos.", "venir", 5, "pret", "My cousins came to visit us.", "pret-ij"),
    ("El guía nos ___ que era peligroso.", "decir", 2, "pret", "The guide told us it was dangerous.", "pret-ij"),
    ("Ella no ___ venir con nosotros.", "querer", 2, "pret", "She wouldn't come with us.", "pret-ij"),
    ("¿Quién ___ la comida?", "traer", 2, "pret", "Who brought the food?", "pret-ij"),
    ("Ayer ___ mucho calor.", "hacer", 2, "pret", "It was very hot yesterday.", "pret-ij"),
    ("Ellos no ___ nada.", "decir", 5, "pret", "They didn't say anything.", "pret-ij"),

    # ---------------------------------------------------------------- imperfect
    ("De niño ___ al fútbol todos los días.", "jugar", 0, "impf", "As a kid I played football every day.", "impf-reg"),
    ("Antes mi abuela ___ en el campo.", "vivir", 2, "impf", "My grandmother used to live in the countryside.", "impf-reg"),
    ("Cuando ___ en Lima, comíamos ceviche cada semana.", "vivir", 3, "impf", "When we lived in Lima, we ate ceviche every week.", "impf-reg"),
    ("¿Tú ___ español en el colegio?", "estudiar", 1, "impf", "Did you study Spanish at school?", "impf-reg"),
    ("Mientras ellos ___, yo leía.", "hablar", 5, "impf", "While they were talking, I was reading.", "impf-reg"),
    ("Mi padre siempre ___ música peruana.", "escuchar", 2, "impf", "My father always listened to Peruvian music.", "impf-reg"),
    ("Antes no ___ coche.", "tener", 3, "impf", "We didn't use to have a car.", "impf-reg"),

    # ---------------------------------------------------------------- imperfect of ser, ir, ver
    ("Cuando ___ niño, vivía en Arequipa.", "ser", 0, "impf", "When I was a child, I lived in Arequipa.", "impf-irr"),
    ("Todos los veranos ___ a la playa.", "ir", 3, "impf", "Every summer we went to the beach.", "impf-irr"),
    ("La casa ___ muy grande.", "ser", 2, "impf", "The house was very big.", "impf-irr"),
    ("¿Tú ___ mucho la tele de pequeño?", "ver", 1, "impf", "Did you watch a lot of TV as a child?", "impf-irr"),
    ("Ellos siempre ___ al mercado los domingos.", "ir", 5, "impf", "They always went to the market on Sundays.", "impf-irr"),
    ("Desde la ventana ___ las montañas.", "ver", 3, "impf", "From the window we could see the mountains.", "impf-irr"),

    # ---------------------------------------------------------------- preterite or imperfect?
    ("Ayer ___ un mensaje de mi madre.", "recibir", 0, "pret", "Yesterday I got a message from my mother.", "pret-impf", "Ayer"),
    ("De niña ___ en un pueblo pequeño.", "vivir", 0, "impf", "As a girl I lived in a small town.", "pret-impf", "De niña"),
    ("Mientras ___ en el bus, empezó a llover.", "viajar", 3, "impf", "While we were travelling on the bus, it started to rain.", "pret-impf", "Mientras"),
    ("Viajábamos en el bus y de repente ___ a llover.", "empezar", 2, "pret", "We were travelling on the bus and suddenly it started to rain.", "pret-impf", "de repente"),
    ("Antes ___ los sábados, pero ahora no.", "trabajar", 0, "impf", "I used to work on Saturdays, but not any more.", "pret-impf", "Antes"),
    ("El año pasado ___ Machu Picchu con mi hermana.", "visitar", 0, "pret", "Last year I visited Machu Picchu with my sister.", "pret-impf", "El año pasado"),
    ("Siempre ___ tarde a clase.", "llegar", 2, "impf", "He was always late for class.", "pret-impf", "Siempre"),
    ("Una vez ___ una alpaca en la calle.", "ver", 0, "pret", "Once I saw an alpaca in the street.", "pret-impf", "Una vez"),
    ("Todos los días ___ en el mismo café.", "comer", 3, "impf", "Every day we used to eat at the same café.", "pret-impf", "Todos los días"),
    ("Anoche ___ diez horas.", "dormir", 0, "pret", "Last night I slept for ten hours.", "pret-impf", "Anoche"),

    # ---------------------------------------------------------------- dar, ver, saber, oír
    ("No ___ dónde está el hotel.", "saber", 0, "pres", "I don't know where the hotel is.", "pres-dar-ver"),
    ("¿Me ___ un mapa, por favor?", "dar", 2, "pres", "Would you give me a map, please?", "pres-dar-ver"),
    ("Desde aquí ___ el lago.", "ver", 0, "pres", "From here I can see the lake.", "pres-dar-ver"),
    ("No te ___, habla más alto.", "oír", 0, "pres", "I can't hear you, speak up.", "pres-dar-ver"),
    ("Ayer le ___ un regalo a mi madre.", "dar", 0, "pret", "Yesterday I gave my mother a present.", "pres-dar-ver"),
    ("¿___ usted el partido anoche?", "ver", 2, "pret", "Did you see the match last night?", "pres-dar-ver"),
    ("¿Ustedes ___ dónde está la parada?", "saber", 5, "pres", "Do you all know where the stop is?", "pres-dar-ver"),

    # ---------------------------------------------------------------- e → i, and -zco
    ("Siempre ___ ceviche en este restaurante.", "pedir", 0, "pres", "I always order ceviche at this restaurant.", "pres-ei-zco"),
    ("No ___ a nadie en Lima.", "conocer", 0, "pres", "I don't know anyone in Lima.", "pres-ei-zco"),
    ("¿Me ___ la pregunta, por favor?", "repetir", 2, "pres", "Could you repeat the question, please?", "pres-ei-zco"),
    ("Yo ___ con cuidado en la montaña.", "conducir", 0, "pres", "I drive carefully in the mountains.", "pres-ei-zco"),
    ("Los turistas ___ al guía.", "seguir", 5, "pres", "The tourists follow the guide.", "pres-ei-zco"),
    ("¿Por qué no le ___ ayuda al guía?", "pedir", 1, "pres", "Why don't you ask the guide for help?", "pres-ei-zco"),

    # ---------------------------------------------------------------- reflexive verbs
    ("¿Cómo ___ su hijo?", "llamarse", 2, "pres", "What's your son's name?", "reflexive"),
    ("Yo ___ Ana.", "llamarse", 0, "pres", "My name is Ana.", "reflexive"),
    ("Nosotros ___ en Cusco tres noches.", "quedarse", 3, "pres", "We're staying in Cusco for three nights.", "reflexive"),
    ("¿Ya ___ tus amigos?", "irse", 5, "pret", "Have your friends left already?", "reflexive"),
    ("No ___ de su nombre.", "acordarse", 0, "pres", "I don't remember his name.", "reflexive"),
    ("Hoy ___ mejor.", "sentirse", 0, "pres", "Today I feel better.", "reflexive"),
    ("Mañana ___ a otra casa.", "mudarse", 3, "near", "Tomorrow we're going to move house.", "reflexive"),

    # ---------------------------------------------------------------- present perfect
    ("Nunca ___ en Perú.", "estar", 0, "perf", "I've never been to Peru.", "perf"),
    ("¿___ usted el ceviche alguna vez?", "probar", 2, "perf", "Have you ever tried ceviche?", "perf"),
    ("Ya ___ las maletas.", "hacer", 3, "perf", "We've already packed our bags.", "perf"),
    ("¿Qué te ___?", "decir", 5, "perf", "What have they told you?", "perf"),
    ("Todavía no ___ la película.", "ver", 0, "perf", "I still haven't seen the film.", "perf"),
    ("¿Tú ___ el correo?", "escribir", 1, "perf", "Have you written the email?", "perf"),
    ("Alguien ___ la ventana.", "abrir", 2, "perf", "Someone has opened the window.", "perf"),

    # ---------------------------------------------------------------- usted / ustedes commands
    ("Por favor, ___ más despacio.", "hablar", 2, "cmd", "Please speak more slowly.", "cmd-usted"),
    ("___ por aquí, señora.", "pasar", 2, "cmd", "Come this way, madam.", "cmd-usted"),
    ("___ la puerta, por favor.", "cerrar", 2, "cmd", "Please close the door.", "cmd-usted"),
    ("___ todo recto hasta la plaza.", "seguir", 2, "cmd", "Go straight on to the square.", "cmd-usted"),
    ("___ aquí un momento, por favor.", "esperar", 6, "cmd", "Wait here a moment, please (all of you).", "cmd-usted"),
    ("No ___ el agua del grifo.", "tomar", 2, "cmd", "Don't drink the tap water.", "cmd-usted"),
    ("___ lo que piensa.", "decir", 2, "cmd", "Say what you think.", "cmd-usted"),

    # ---------------------------------------------------------------- tú commands
    ("___ más despacio, por favor.", "hablar", 0, "cmd", "Speak more slowly, please.", "cmd-tu"),
    ("___ aquí un momento.", "venir", 0, "cmd", "Come here a moment.", "cmd-tu"),
    ("___ la mochila en el suelo.", "poner", 0, "cmd", "Put the backpack on the floor.", "cmd-tu"),
    ("___ la verdad.", "decir", 0, "cmd", "Tell the truth.", "cmd-tu"),
    ("___ cuidado con el bolso.", "tener", 0, "cmd", "Be careful with your bag.", "cmd-tu"),
    ("___ la tarea antes de salir.", "hacer", 0, "cmd", "Do your homework before you go out.", "cmd-tu"),
    ("___ esta sopa, está muy rica.", "probar", 0, "cmd", "Try this soup, it's delicious.", "cmd-tu"),

    # ---------------------------------------------------------------- present subjunctive, regular
    ("Quiero que ___ conmigo.", "bailar", 1, "subj", "I want you to dance with me.", "subj-reg"),
    ("Es importante que ___ mucha agua.", "beber", 1, "subj", "It's important that you drink a lot of water.", "subj-reg"),
    ("Ojalá me ___ pronto.", "escribir", 2, "subj", "I hope she writes to me soon.", "subj-reg"),
    ("Mi madre quiere que ___ más.", "estudiar", 0, "subj", "My mother wants me to study more.", "subj-reg"),
    ("No creo que ellos ___ aquí.", "vivir", 5, "subj", "I don't think they live here.", "subj-reg"),
    ("Necesito que me ___.", "ayudar", 2, "subj", "I need you to help me.", "subj-reg"),
    ("Es mejor que ___ algo ligero.", "comer", 3, "subj", "It's better if we eat something light.", "subj-reg"),

    # ---------------------------------------------------------------- subjunctive from the yo form
    ("Quiero que ___ a mi casa.", "venir", 1, "subj", "I want you to come to my house.", "subj-irr"),
    ("Ojalá ___ sol mañana.", "hacer", 2, "subj", "I hope it's sunny tomorrow.", "subj-irr"),
    ("Espero que ___ bien.", "estar", 5, "subj", "I hope you're all well.", "subj-irr"),
    ("No creo que ella ___ la respuesta.", "saber", 2, "subj", "I don't think she knows the answer.", "subj-irr"),
    ("Cuando ___ a Lima, llámame.", "llegar", 1, "subj", "When you get to Lima, call me.", "subj-irr"),
    ("No ___ eso.", "decir", 1, "cmd", "Don't say that.", "subj-irr"),
    ("Es posible que ___ un problema.", "haber", 2, "subj", "There may be a problem.", "subj-irr"),
    ("Quiero que ___ feliz.", "ser", 1, "subj", "I want you to be happy.", "subj-irr"),

    # ---------------------------------------------------------------- subjunctive or not?
    ("Creo que el museo ___ los lunes.", "cerrar", 2, "pres", "I think the museum closes on Mondays.", "subj-or-not", "Creo que"),
    ("No creo que el museo ___ hoy.", "abrir", 2, "subj", "I don't think the museum opens today.", "subj-or-not", "No creo que"),
    ("Normalmente ___ café a las siete.", "tomar", 0, "pres", "I normally have coffee at seven.", "subj-or-not", "Normalmente"),
    ("Quiero que me ___ mañana.", "llamar", 1, "subj", "I want you to call me tomorrow.", "subj-or-not", "Quiero que"),
    ("Sé que ___ razón.", "tener", 1, "pres", "I know you're right.", "subj-or-not", "Sé que"),
    ("Ojalá ___ razón.", "tener", 1, "subj", "I hope you're right.", "subj-or-not", "Ojalá"),
    ("Cuando ___ a casa, te escribo.", "llegar", 0, "subj", "When I get home, I'll write to you.", "subj-or-not", "Cuando"),
    ("Cuando ___ a casa, siempre ceno.", "llegar", 0, "pres", "When I get home, I always have dinner.", "subj-or-not", "siempre"),
    ("Hablo despacio para que me ___.", "entender", 5, "subj", "I speak slowly so that you all understand me.", "subj-or-not", "para que"),
    ("Ahora ___ en Cusco.", "vivir", 0, "pres", "Now I live in Cusco.", "subj-or-not", "Ahora"),

    # ---------------------------------------------------------------- future
    ("Mañana ___ en la sierra.", "llover", 2, "fut", "Tomorrow it will rain in the mountains.", "fut"),
    ("El año que viene ___ a Perú.", "viajar", 0, "fut", "Next year I'll travel to Peru.", "fut"),
    ("¿Dónde ___ mis llaves?", "estar", 5, "fut", "Where could my keys be?", "fut"),
    ("Te lo ___ mañana.", "decir", 0, "fut", "I'll tell you tomorrow.", "fut"),
    ("¿___ tiempo el viernes?", "tener", 1, "fut", "Will you have time on Friday?", "fut"),
    ("Algún día ___ español muy bien.", "hablar", 0, "fut", "One day I'll speak Spanish very well.", "fut"),
    ("El último bus ___ a las once.", "salir", 2, "fut", "The last bus will leave at eleven.", "fut"),

    # ---------------------------------------------------------------- preterite: pidió, durmió
    ("El niño ___ toda la noche.", "dormir", 2, "pret", "The child slept all night.", "pret-stem-ir"),
    ("¿Qué ___ ustedes?", "pedir", 5, "pret", "What did you all order?", "pret-stem-ir"),
    ("Ella ___ frío en la montaña.", "sentir", 2, "pret", "She felt cold on the mountain.", "pret-stem-ir"),
    ("Mis abuelos ___ hace años.", "morir", 5, "pret", "My grandparents died years ago.", "pret-stem-ir"),
    ("El guía ___ las instrucciones.", "repetir", 2, "pret", "The guide repeated the instructions.", "pret-stem-ir"),
    ("Ellos ___ el camino del río.", "seguir", 5, "pret", "They followed the river path.", "pret-stem-ir"),

    # ---------------------------------------------------------------- conditional
    ("Yo que tú, no ___ el agua del grifo.", "beber", 0, "cond", "If I were you, I wouldn't drink the tap water.", "cond"),
    ("¿Qué ___ tú con un millón de dólares?", "hacer", 1, "cond", "What would you do with a million dollars?", "cond"),
    ("Con más tiempo, ___ el lago Titicaca.", "visitar", 3, "cond", "With more time, we'd visit Lake Titicaca.", "cond"),
    ("Me dijo que ___ a las ocho.", "venir", 2, "cond", "He told me he would come at eight.", "cond"),
    ("¿___ usted repetirlo?", "poder", 2, "cond", "Could you repeat it?", "cond"),
    ("Me ___ vivir aquí.", "gustar", 2, "cond", "I'd like to live here.", "cond"),
    ("Ellos no ___ qué hacer.", "saber", 5, "cond", "They wouldn't know what to do.", "cond"),

    # ---------------------------------------------------------------- imperfect subjunctive
    ("Si ___ más tiempo, iría a la selva.", "tener", 0, "impsubj", "If I had more time, I'd go to the jungle.", "impsubj"),
    ("Si ___ tú, no lo haría.", "ser", 0, "impsubj", "If I were you, I wouldn't do it.", "impsubj"),
    ("Quería que ___ conmigo.", "venir", 1, "impsubj", "I wanted you to come with me.", "impsubj"),
    ("Me pidió que ___ la puerta.", "cerrar", 0, "impsubj", "She asked me to close the door.", "impsubj"),
    ("Habla como si lo ___ todo.", "saber", 2, "impsubj", "He talks as if he knew everything.", "impsubj"),
    ("Si ___, viajaríamos más.", "poder", 3, "impsubj", "If we could, we'd travel more.", "impsubj"),

    # ---------------------------------------------------------------- pluperfect
    ("Cuando llegué, el tren ya ___.", "salir", 2, "plup", "When I arrived, the train had already left.", "plup"),
    ("Nunca ___ algo tan bonito.", "ver", 0, "plup", "I had never seen anything so beautiful.", "plup"),
    ("Ellos ya ___ cuando llamamos.", "comer", 5, "plup", "They had already eaten when we called.", "plup"),
    ("¿___ tú en Perú antes?", "estar", 1, "plup", "Had you been to Peru before?", "plup"),
    ("Me dijo que lo ___ ella.", "hacer", 2, "plup", "She told me she had done it herself.", "plup"),
    ("Nosotros ya ___ la cuenta.", "pagar", 3, "plup", "We had already paid the bill.", "plup"),

    # ---------------------------------------------------------------- less common changes
    ("Los domingos ___ al fútbol con mis amigos.", "jugar", 0, "pres", "On Sundays I play football with my friends.", "rare"),
    ("¿Tú ___ al ajedrez?", "jugar", 1, "pres", "Do you play chess?", "rare"),
    ("Te ___ las fotos mañana.", "enviar", 0, "pres", "I'll send you the photos tomorrow.", "rare"),
    ("Ellos ___ una casa nueva.", "construir", 5, "pres", "They're building a new house.", "rare"),
    ("Yo ___ la cámara de la lluvia.", "proteger", 0, "pres", "I protect the camera from the rain.", "rare"),
    ("El camino ___ hasta el lago.", "continuar", 2, "pres", "The path goes on to the lake.", "rare"),
    ("Los niños ___ mucho.", "reír", 5, "pres", "The children laugh a lot.", "rare"),

    # ---------------------------------------------------------------- rare compound tenses (read only)
    ("Para el viernes ya ___ el trabajo.", "terminar", 0, "futperf", "By Friday I'll have finished the work.", "read-only"),
    ("Con más tiempo, ___ más cosas.", "ver", 3, "condperf", "With more time, we would have seen more.", "read-only"),
    ("Espero que ___ bien al hotel.", "llegar", 5, "subjperf", "I hope they've got to the hotel safely.", "read-only"),
    ("Si lo ___, te lo habría dicho.", "saber", 0, "plupsubj", "If I had known, I'd have told you.", "read-only"),
    ("Ojalá ___ con nosotros.", "venir", 1, "plupsubj", "I wish you had come with us.", "read-only"),
    ("A estas horas ya ___ a Cusco.", "llegar", 5, "futperf", "By now they'll have arrived in Cusco.", "read-only"),
]

# The tenses each unit's sentences may use (the units themselves are in VerbUnits.List.cs).
UNIT_TENSES = {
    "pres-ar": {"pres"}, "ser-estar": {"pres"}, "pres-er-ir": {"pres"}, "ir-going": {"pres", "near"},
    "pres-go": {"pres"}, "pres-stem": {"pres"}, "polite": {"impsubj", "cond"}, "pret-ar": {"pret"},
    "pret-ser-ir": {"pret"}, "pret-er-ir": {"pret"}, "pret-u": {"pret"}, "pret-ij": {"pret"},
    "impf-reg": {"impf"}, "impf-irr": {"impf"}, "pret-impf": {"pret", "impf"}, "pres-dar-ver": {"pres", "pret"},
    "pres-ei-zco": {"pres"}, "reflexive": {"pres", "near", "pret", "impf"}, "perf": {"perf"},
    "cmd-usted": {"cmd"}, "cmd-tu": {"cmd"}, "subj-reg": {"subj"}, "subj-irr": {"subj", "cmd"},
    "subj-or-not": {"subj", "pres"}, "fut": {"fut"}, "pret-stem-ir": {"pret"}, "cond": {"cond"},
    "impsubj": {"impsubj"}, "plup": {"plup"}, "rare": {"pres", "pret", "impf"},
    "read-only": {"futperf", "condperf", "subjperf", "plupsubj"},
}

HABER_TENSE = {"perf": "pres", "plup": "impf", "futperf": "fut", "condperf": "cond", "subjperf": "subj", "plupsubj": "impsubj"}
IR = ["voy", "vas", "va", "vamos", "vais", "van"]
PRON = ["me", "te", "se", "nos", "os", "se"]

verbs = {v["inf"]: v for v in json.load(open("SpanishFlashcards/wwwroot/data/verbs.json", encoding="utf-8"))["verbs"]}


def forms(inf, t, p):
    """Every accepted spelling of a form, like Verb.Answers in the app (the -se forms aside)."""
    v = verbs[inf]
    refl = inf.endswith("se")
    bare = inf[:-2] if refl else inf
    if t == "near":
        if v.get("only3") and p != 2:
            return []
        return [f"{PRON[p]} {IR[p]} a {bare}", f"{IR[p]} a {bare}{PRON[p]}"] if refl else [f"{IR[p]} a {bare}"]
    if t in HABER_TENSE:
        aux = verbs["haber"]["f"][HABER_TENSE[t]][p]
        if aux is None or (v.get("only3") and p != 2):
            return []
        pron = PRON[p] + " " if refl else ""
        return [f"{pron}{aux.split('|')[0]} {v['pp']}"]
    f = v["f"].get(t)
    if not f or p >= len(f) or f[p] is None:
        return []
    return f[p].split("|")


bad = 0
seen = set()
for row in S:
    es, inf, p, t, en, unit = row[:6]
    clue = row[6] if len(row) > 6 else None
    alt = row[7] if len(row) > 7 else None
    problems = []
    if es.count("___") != 1:
        problems.append("needs exactly one ___")
    if inf not in verbs:
        problems.append(f"no verb {inf}")
    elif not forms(inf, t, p):
        problems.append(f"no form {t} {p}")
    if unit not in UNIT_TENSES:
        problems.append(f"no unit {unit}")
    elif t not in UNIT_TENSES[unit]:
        problems.append(f"tense {t} not in {unit}")
    if clue and clue.split(",")[0].lower() not in es.lower():
        problems.append(f"clue {clue!r} not in the sentence")
    if es in seen and unit != "subj-or-not":
        problems.append("duplicate")
    seen.add(es)
    shown = es.replace("___", "[" + (forms(inf, t, p) or ["?"])[0] + "]") if inf in verbs else es
    print(f"{unit:12} {shown:62} | {en}" + (f"  !! {'; '.join(problems)}" if problems else ""))
    bad += len(problems) > 0

print(f"bad {bad}, total {len(S)}")
if bad:
    sys.exit(1)


def cs(s):
    return "null" if s is None else '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'


lines = [
    "// Generated by tools/verb-sentences/sentences_src.py. Edit the sentences there and run it again.",
    "namespace SpanishFlashcards.Models.Verbs;",
    "",
    "public static partial class VerbSentences",
    "{",
    "    public static readonly IReadOnlyList<VerbSentence> All =",
    "    [",
]
for row in S:
    es, inf, p, t, en, unit = row[:6]
    clue = row[6] if len(row) > 6 else None
    alt = row[7] if len(row) > 7 else None
    lines.append(f"        new({cs(es)}, {cs(inf)}, {p}, {cs(t)}, {cs(en)}, {cs(unit)}, {cs(clue)}, {cs(alt)}),")
lines += ["    ];", "}", ""]
open("SpanishFlashcards/Models/Verbs/VerbSentences.Data.cs", "w", encoding="utf-8").write("\n".join(lines))
