# Il documento operativo: che cosa fa quello di va.ivao.aero, e che cosa vale prendere

**Data:** 10 settembre 2026 — Carmine ha collaudato il sistema dei documenti dell'hub e ha detto,
dopo essersi consultato con altri membri dello staff della divisione italiana, che preferirebbe «un
meccanismo come quello di va.ivao.aero», con qualche miglioramento.

**Stato:** **decisa da Carmine il 10 settembre 2026**, sulle due domande di §5; **costruita nella
notte fra l'11 e il 12 settembre** come G14, prima passata completa in cinque commit sul branch
`m1/g14-operational-document` (sezione G14 di `04-piano-implementazione-m1.md`, piano 0.65). Questa
nota esiste perché il grosso di ciò che segue era **(c)** di piano
§16.E — meccanismi che non ci sono — e §16.E dice che ci si ferma, si scrive mezza pagina, si decide
con Carmine, si aggiorna il piano, e solo dopo si codifica.

> **Le due decisioni:**
>
> 1. **Quando: prima il tag, poi G14.** M1 si chiude come previsto — Carmine rifà `tools/demo-m1.md`,
>    mergia la PR #57, esce `v0.2.0-m1` — e il documento operativo diventa **G14**, una fase sua.
>    Il resto della paperasse (piano §9.3, design M1, `04-piano-implementazione-m1.md` §30, HANDOFF)
>    si scrive **all'apertura di G14, dopo il merge**: farla adesso vorrebbe dire toccare `HANDOFF.md`
>    e i piani su un ramo che è esattamente la PR #57, cioè rimettere le mani nella PR che chiude M1.
> 2. **La prima passata:** il **tipo** SOP/LoA (3.1), i **sei campi operativi** (3.2), **`Archived` e
>    `Superseded` col successore** (3.3), i blocchi **Frequency Table** e **Coordination** (3.4), e il
>    **piè di pagina con la stampa** (3.6) — cioè i punti 1 e 3 della nota del 9 settembre, che qui
>    trovano finalmente la fase che li costruisce.
>
> **Fuori dalla prima passata, in quest'ordine:** *METAR* e *Runway Config (Live)* (3.5) — non più
> bloccati, e sono la cosa che di quella pagina si vede di più; poi *Airspace/Sector*, *Procedure
> Steps* e *Reference List*, guardando la resa dei primi due. E la **clonazione** (3.7), che è (b).

**Come è stato guardato:** nel browser di Carmine, già autenticato, il 10 settembre 2026, **in sola
lettura**: `backend/` (elenco dei moduli), `backend/loasop/` (la lista), `backend/loasop/editor.php?id=8`
(l'editor) e `sop/klax-tower-sop` (la resa pubblica). Nessun salvataggio, nessun clic su
`Unpublish`, `Clone` o `deleteDoc()`, nessun trascinamento; l'indicatore diceva ancora «Saved» alla
fine. La tavolozza e i campi sono stati **letti dal DOM**, non dedotti da uno screenshot.

⚠️ **Che cosa non è stato verificato:** il loro modello dati. Tutto ciò che segue sul *come* lo
salvano è inferito dall'interfaccia. Ciò che si afferma qui è che cosa fa il loro prodotto, non come
è fatto dentro.

---

## 1. La cosa più importante: non stanno parlando dello stesso documento

Da loro «documenti» non è una voce del CMS: è un modulo a sé, `backend/loasop/`, intitolato **LoA/SOP
Documents** — *Letters of Agreement* e *Standard Operating Procedures*. È il documento **operativo di
un controllore**: chi parla con chi, su quale frequenza, a quale quota si passa il traffico.

Da noi un documento è, oggi, `kind = Document` in `cms_contents`: **una pagina con eventualmente un
file allegato** (G5, §19 del piano di implementazione). È esatto e non è sbagliato — ma risponde a
un'altra domanda. Il collaudo di Carmine ha trovato questo, non un difetto.

**Quindi la richiesta non è «rifare i documenti»: è aggiungere il documento operativo.** Ed è la
ragione per cui quasi nulla di ciò che segue butta via qualcosa che abbiamo.

## 2. Il censimento, prima di stimare qualunque cosa

La loro tavolozza ha **22 blocchi in cinque famiglie**, letti dal DOM:

| Famiglia | I loro blocchi | Da noi |
|---|---|---|
| Content | Text, Hero, Image, Video, Embed | **tutti e cinque** |
| Procedures | Accordion/FAQ, Tabs | **tutti e due** |
| Data | Table, Stats, Card Grid, Icon Grid | **tutti e quattro** |
| Format | Alert/Notice, Divider, Spacer, Columns | **tutti e quattro** (`callout`, `divider`, `spacer`, e le colonne sono la riga) |
| Procedures | Procedure Steps, Reference List | **no** — ma `timeline` e `linkList` ci vanno vicino |
| ATC Data | Frequency Table, Airspace/Sector, Coordination, Runway Config (Live), METAR | **nessuno dei cinque** |

E il resto dell'impalcatura ce l'abbiamo **già**, non «quasi»:

- **Sezione → Riga → Blocco**: preso da loro il 10 settembre stesso (`46e243a`), con la spaziatura e
  l'allineamento sulla riga. La loro riga e la nostra ora dicono la stessa cosa;
- **versioni con changelog alla pubblicazione**: `cms_content_versions` dal M0, e la loro finestra
  «Publish Version → Changelog (optional)» è la nostra;
- **categorie per dipartimento**: `cms_categories` (G5). Le loro sono Tower / Approach / Center /
  Ground / FIR / General, cioè esattamente righe che un coordinatore scriverebbe da solo;
- **bozza / pubblicato, visibilità, media library, anteprima**: tutte cose di M0 e M1.

**Conclusione del censimento: di 22 blocchi, ce ne mancano 7, e cinque sono la stessa famiglia.**

## 3. Le differenze vere, con il costo e la classificazione §16.E

### 3.1 Il tipo del documento — **(b), una colonna**

Da loro **Type** (SOP / LoA) è una colonna, ed è **ortogonale** alla categoria: esiste un *Tower SOP*
e potrebbe esistere un *Tower LoA*. Da noi `kind` è già occupato dal discriminante di `cms_contents`
(Page, News, Document, Template, Dashboard), e `Category` è già la categoria.

Serve **una colonna nullable in più**, e non un secondo `kind`: moltiplicare i `kind` moltiplica le
liste del back-office, e G5 ha appena dimostrato che tre liste sono **una schermata montata tre
volte**. Migrazione additiva, un campo nello schema zod, una colonna nella lista.

### 3.2 I metadati operativi — **(b), sei colonne e un pannello**

Il loro pannello «Document Metadata», letto campo per campo:

| Campo | Valore visto | Nota |
|---|---|---|
| Status | Draft / Published / **Archived** / **Superseded** | vedi 3.3 |
| Category | Tower | ce l'abbiamo |
| Primary Position | `KLAX_TWR` | testo libero da loro |
| Secondary Position | `KLAX_APP` | testo libero da loro |
| ICAO Airport | `KLAX` | «Aeroview activates automatically when ICAO is set» |
| FIR Code | `KZLA` | testo libero da loro |
| Effective Date | 21/05/2026 | |
| Review Date | 21/01/2028 | mostrata e basta |

⚠️ **Qui c'è il primo miglioramento vero, ed è gratis**: da loro sono **cinque input di testo**. Da
noi `ref_ivao_airports` e `ref_ivao_centers` ci sono già, sincronizzati dall'API IVAO; ICAO, FIR e
posizioni si **scelgono da un elenco** invece di digitarli. Un ICAO scritto storto è un documento che
non si trova più, e sono le uniche cinque caselle di tutto il pannello dove un errore di battitura
non si vede.

### 3.3 `Archived` e `Superseded` — **(c) piccola, ma è (c)**

Oggi da noi un contenuto è bozza o pubblicato, più la visibilità. **Archiviato** e **sostituito da**
non esistono, e non sono la stessa cosa: archiviato è «non vale più», sostituito è «non vale più,
**vale quest'altro**».

⚠️ **E da loro `Superseded` non dice da cosa.** È un vicolo cieco: il lettore trova un documento
scaduto e nessuna strada. **Secondo miglioramento**: una colonna che punta al successore, e il
renderer che mette in cima l'avviso con il collegamento. Senza quella colonna, `Superseded` è solo
`Archived` scritto in un altro modo, e tanto varrebbe non averlo.

### 3.4 I cinque blocchi ATC — **(c), ed è il grosso del lavoro**

**Frequency Table** (visto): callsign, frequenza, tipo con la pastiglia colorata, CPDLC, **rating
minimo con il distintivo IVAO**. **Coordination** (visto): posizione → posizione, punto di
trasferimento, quota, direzione, e una riga di prosa sotto. **Airspace / Sector** e **METAR**: nella
tavolozza, non usati nel documento guardato.

⚠️ **Domanda del perimetro** (`CLAUDE.md` §3, piano §4.2): *questo nomina IVAO?* Il rating minimo con
il distintivo, sì. Le frequenze, i settori e il METAR no: sono aviazione, che è il dominio del
prodotto, non IVAO. La regola non cambia — un blocco non parla con l'API IVAO da sé, chiede a un
provider di blocco Data, e l'unico che parla con IVAO resta `IIvaoApiClient`.

**Terzo miglioramento, e vale più di quanto costa:** da loro la tabella delle frequenze si compila a
mano. Da noi, con l'ICAO scelto da un elenco, può **nascere precompilata** dalle posizioni che quella
divisione ha davvero.

### 3.5 Le configurazioni di pista e il blocco vivo — **(c), ed è la parte che va decisa prima**

È la cosa più vistosa della loro pagina pubblica: un riquadro «Aeroview» che legge il **METAR vero**,
sceglie la configurazione di pista attiva e calcola per ogni pista **vento in testa, al traverso e in
coda**, diviso fra arrivi e partenze.

L'editor ha un sotto-editor per le configurazioni, letto campo per campo: nome («West Ops»), priorità,
colore, «Default (fallback)», **direzione del vento da / a in gradi**, vento minimo / massimo, raffica
massima, **finestra oraria UTC di attività**, e le caselle delle piste in arrivo e in partenza.

**La notizia buona, misurata e non supposta:** le piste ce le abbiamo già. `ref_ivao_airports` porta
`runways_json`, sincronizzato dall'API IVAO, e dentro c'è quello che serve al conto:

```json
{"runway":"RW17","length":4593,"bearing":170,"latitude":42.93,"longitude":12.71,"elevation":730,"width":30}
```

Nel database di sviluppo: **221 aeroporti, 114 con le piste**. Il calcolo del vento al traverso è
trigonometria su `bearing` e la direzione del vento — una funzione, non un servizio.

**Il METAR non ce l'abbiamo.** In tutto il repository la parola compare **una volta sola**, in piano
§9.3, e come *analogia* («uno stato della rete congelato è un dato scaduto spacciato per attuale, la
stessa regola del METAR in vIPI»), non come funzione.

**Ma l'API IVAO lo espone, e questo lo ho misurato**, non supposto. `GET /v2/airports/{icao}/metar`
risponde **401 `not_authenticated`** con il corpo d'errore strutturato — mentre una rotta che non
esiste (`/v2/definitely-not-a-route-xyz`, `/v2/metar/LIRF`) risponde **404 con il corpo vuoto**. Le
due risposte sono diverse, quindi la rotta c'è: manca solo il token, che
`IvaoApiTokenProvider` procura già.

⚠️ **Non ho verificato la forma della risposta** — servirebbe una chiamata autenticata, che non ho
fatto. Va guardata prima di scrivere il parser: se torna il METAR grezzo va decodificato (vento,
raffica), se torna decodificato no.

**Conseguenza:** questo è **un metodo in più su `IIvaoApiClient`** e un provider di blocco Data, non
una dipendenza esterna nuova. Il pezzo che sembrava il più caro dei cinque non lo è.

Coerenza con quanto già deciso: il blocco è **sempre `live`**, come `networkStats`, e per la stessa
ragione già scritta in piano §9.3 — un vento congelato è un dato scaduto spacciato per attuale.

### 3.6 Il piè di pagina e la stampa — **già decisi il 9 settembre**

In fondo alla loro pagina pubblica: `SOP · Version 2 · Published 22 May 2026 · [Print Document]`.

È, parola per parola, la nota `2026-09-09-il-documento-dice-di-se.md`: il **punto 1** (il piè di
pagina, **deciso e non costruito**) e il **punto 3** (la stampa, **parcheggiata**). Non c'è niente da
ridecidere; c'è da costruirli, e vanno costruiti insieme perché toccano lo stesso renderer. La
trappola scritta lì resta la trappola: `tabs` e `accordion` **nascondono contenuto**, e su carta
devono essere aperti.

Loro il **punto 2** — la pubblicazione programmata — non ce l'hanno nel modulo documenti (il modulo
News la dichiara: «drafts, categories, scheduling and RSS»). Resta parcheggiata, e resta (c).

### 3.7 Le due cose piccole

- **Clonare un documento** (`cloneDoc(id, titolo)` nella lista). Un SOP di torre somiglia molto al SOP
  di torre dell'aeroporto accanto. **(b)**, e il motore CRUD c'è.
- **L'indirizzo pubblico per tipo**: `/sop/<slug>`, non `/documents/<slug>`. ⚠️ Qui **si dice di no**:
  l'ultima richiesta di G13 è stata che l'indirizzo di una voce di menu sia un **insieme chiuso**,
  «perché ogni indirizzo che esce dal sito deve vivere in una tabella sola». Un secondo albero di
  indirizzi per un secondo tipo di documento va contro una decisione presa dieci giorni fa. Restano
  `/documents/<slug>`, con il tipo filtrabile.

## 4. I miglioramenti, raccolti

Carmine ha chiesto «se puoi aggiungici anche qualche miglioramento». I primi tre sono nel testo qui
sopra e non li ripeto: **ICAO / FIR / posizioni scelti da un elenco** invece che digitati (3.2),
**`Superseded` che dice da cosa** (3.3), **la tabella delle frequenze che nasce precompilata** (3.4).
Gli altri tre:

4. **La data di revisione deve fare qualcosa.** Da loro è una casella che si guarda. Da noi Quartz
   gira già con due job e il servizio notifiche c'è da G7: alla scadenza il dipartimento proprietario
   riceve un avviso, e la lista del back-office ha il filtro «da rivedere». Un SOP scaduto che nessuno
   sa che è scaduto è il modo normale in cui questi documenti marciscono. **(b)**.
5. **La data di efficacia deve dirsi al lettore.** `Effective Date` nel futuro vuol dire «questo entra
   in vigore il 21 maggio»: il pubblico deve leggerlo in cima, non doverlo dedurre dal pannello dello
   staff. **(a)**, ed è il fratello povero della pubblicazione programmata: se un giorno si costruisce
   quella, le due devono usare la stessa colonna e non due.
6. **La prosa tradotta, il resto no.** Dal 9 settembre l'estrattore per la ricerca indicizza **solo**
   ciò che sta dentro una mappa `Localized`. Un callsign, una frequenza e una quota **non sono prosa**
   e non vanno tradotti; la riga di nota sotto un blocco Coordination sì. È la regola che c'è già:
   qui va solo applicata bene, blocco per blocco, alla prima scrittura invece che dopo.

## 5. Che cosa andava deciso prima di scrivere una riga

1. ~~**Quando.**~~ **Decisa: prima il tag, poi G14** (vedi il riquadro in testa). M1 era ferma sul
   filo — il tag `v0.2.0-m1` aspetta che Carmine rifaccia la scheda e mergi la PR #57 — e questo
   lavoro non è una rifinitura di G13: è una fase.
2. ~~**Il METAR.**~~ **Chiusa mentre si scriveva questa nota**: `/v2/airports/{icao}/metar` esiste
   (3.5). Resta da guardare la forma della risposta con una chiamata autenticata, ma non è più un
   bivio: è un metodo su `IIvaoApiClient`.
3. **Dove vivono i cinque blocchi ATC.** Raccomandazione: nel **registry del nucleo**, con gli altri
   27, perché si attaccano a `cms_contents` che è del nucleo e oggi nessun modulo possiede le
   procedure ATC. Quando arriveranno M3 e M4 potranno registrarne altri: è già il meccanismo.
4. ~~**Quanti dei sette blocchi mancanti nella prima passata.**~~ **Decisa**, ed è la raccomandazione:
   **Frequency Table** e
   **Coordination** — sono i due che compongono davvero il documento guardato, e insieme al piè di
   pagina e alla stampa fanno un documento operativo completo. *Airspace/Sector*, *Procedure Steps*
   e *Reference List* per ultimi, guardando la resa. *METAR* e *Runway Config* subito dopo i primi
   due: ora si sa che si possono fare, e sono la cosa che di quella pagina si vede di più.

## 6. Che cosa non prendere

- **`/sop/<slug>`** — 3.7, va contro la decisione dell'8 settembre sugli indirizzi.
- **Il testo libero nei metadati** — 3.2: abbiamo gli elenchi veri, digitare un ICAO è un difetto.
- **`Superseded` senza successore** — 3.3: o punta a qualcosa, o non serve.
- **Un secondo editor.** Il loro editor LoA/SOP **è** il loro page builder con un pannello diverso a
  sinistra e cinque blocchi in più nella tavolozza: stessa barra, stesse sezioni, stesse righe, stesso
  «Click a block to edit its properties». È la conferma che la strada presa il 9 e il 10 settembre era
  quella giusta, ed è anche il vincolo: `NoSecondContentEntity` di G5 resta verde, o questa nota ha
  sbagliato strada.
