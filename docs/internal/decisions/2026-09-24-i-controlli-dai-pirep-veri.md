# I controlli dai PIREP veri (prima di T17)

**Data:** 24 settembre 2026 — preparazione di T17 di M2
**Stato:** **decisa** (Carmine, 24 settembre 2026: le risposte in §4, nessun punto da aggiungere in §6). Piano 1.01; design §6.4 e fasi
T17, T18 e T21 aggiornati.
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: controlli nuovi nel catalogo di T9 (`CheckCatalog`) e un parametro che cambia forma
(`equipment`). Nessuna estensione del nucleo.

## 1. Da dove viene

Prima di scrivere il motore dei controlli, Carmine ha chiesto di guardare **che cosa trovano oggi i controllori** nel sistema dei tour in
uso (`tours.th.ivao.aero`, divisione IT), da amministratore, in sola lettura:

- **45 PIREP da 12 tour del 2026** (da giugno): per ogni tour due respinti, uno «da modificare» se c'era, due accettati;
- **363 commenti dei controllori**, dallo storico dei piloti di quei PIREP (la pagina di un PIREP mostra tutte le leg del pilota, in tutti i
  tour); tolte le battute fra controllori sui propri PIREP;
- i **commenti predefiniti** (`valitexte`) e il **regolamento generale** (GR1–GR17, RR1–RR5).

Il vecchio sistema **ricalcola i controlli dal tracker a ogni apertura**, e il tracker IVAO tiene circa tre mesi: 22 PIREP su 45 non
mostrano più nessun controllo. Da noi non succede, perché da T13a le tracce si salvano all'invio.

## 2. I controlli automatici di oggi

| Controllo di oggi (soglia) | Da noi |
|---|---|
| Volo trovato nel tracker | Non serve: il pilota sceglie il volo dal tracker (T11b) |
| Decollo e atterraggio dichiarati entro ±15 min dal tracker | Non serve, per lo stesso motivo |
| Callsign del pilota | `callsign` |
| Categoria di scia dell'aereo | `aircraft`, con i gruppi di aerei, se il FOD li configura |
| IAS sotto FL100 (265 kt, cioè 250 + 15) | `speed250` (T18) |
| «Speed was ok (300 below 6.000 ft)» | Non chiaro che cosa misuri: da chiedere |
| Quota massima (19.500 ft nel VFR, 66.000 negli altri) | **`maxAltitude`**, nuovo (§4) |
| Alternato presente | `alternate` |
| Regole di volo del piano | **`flightRules`**, nuovo (§4) |
| Tre minuti fermi (primi e ultimi 3 punti a 0 kt) | `parking` (T18) |
| Decollo o atterraggio sull'aeroporto sbagliato | `landingAtArrival`; la partenza la rifiuta già l'invio (`reportFlightDeparture`) |
| «Nessun piano corretto al decollo» | **`planAtTakeoff`**, nuovo (§4) |
| Disconnessione oltre 15 min | `disconnections` (T18) |
| Sim rate aumentato | `simRate` (T18) |

Falsi positivi del vecchio sistema, da tenere come casi di prova per T18: **880159** dà insieme «disconnesso 2921 minuti», «decollato
dall'aeroporto sbagliato» e «atterrato all'aeroporto sbagliato» (sessioni di giorni diversi fuse); il controllore li ha ignorati.
Falsi negativi: **882171** «Route seems to be ok» con dei DCT nel piano VFR; **879788, 879691, 879558** tutto verde, e respinti o
ammoniti per Y mancante, IFPS e procedure.

## 3. Che cosa trovano i controllori a mano

Frequenze da una classificazione per parole chiave sui 363 commenti: **indicative**.

| # | Errore | Circa | Da noi |
|---|---|---|---|
| 1 | Piano che non passa la validazione IFPS (Eurocontrol) | 95 | agente o umano: vuole il NOP. Rifiuto al quarto avviso |
| 2 | Procedure volate non riportate (RR5) | 47 | già coperto: SID, STAR e avvicinamento sono campi del form, obbligatori con `RequiresProcedures` |
| 3 | Vincoli di SID e STAR (velocità, quote) | 45 | agente (Navigraph), «più avanti» nel design §6.6 |
| 4 | Procedura dichiarata ma non volata (orbite, racetrack, diretti) | 35 | agente |
| 5 | Equipaggiamento (Y mancante, tutti mancanti, transponder, PBN, Z senza COM/DAT/NAV) | 36 | `equipment` con la forma nuova (§4) e `flightPlanForm` |
| 6 | 250 kt sotto FL100 | 25 | `speed250` |
| 7 | Dati del report sbagliati (callsign, orari, data, leg sbagliata) | 22 | scompare per costruzione (il volo lo sceglie il pilota dal tracker) |
| 8 | Istruzioni della leg: touch and go, pista obbligatoria, VRP, rotta lungo la costa | 21 | **rimandato**: manovre obbligatorie, «dopo il sistema principale» (`05-design-m2.md`, l'elenco di ciò che resta fuori; nota `2026-09-14-requisiti-dei-tour`) |
| 9 | Livello pianificato diverso da quello volato (step climb, RFL) | 18 | agente: le salite sono pianificate sui fix (§4) |
| 10 | Campo 18 e forma del piano (REG/, RMK/, PER/, IVAOVA/) | 14 | `flightPlanForm` |
| 11 | SID e STAR scritte nella rotta fuori da DE e AT | 12 | `flightPlanForm` |
| 12 | Parcheggio 3 minuti | 12 | `parking` |
| 13 | Semicircolari | 11 | agente, `semicircularLevels` |
| 14 | STAR «by ATC» senza ATC | 11 | agente più `atcCoverage` |
| 15 | Regole di volo sbagliate (IFR nel VFR, I invece di Y, tipo X) | 10+ | `flightRules` |
| 16 | Aereo non adatto alla leg (London City, Melilla, A380) | 10 | `aircraft` con gli aerei per leg (T7a) |
| 17 | Alternato mancante, uguale alla destinazione, troppo lontano | 9 | `alternate` (l'ultimo resta un giudizio) |
| 18 | Decollo da un raccordo senza distanze dichiarate | 3 | `takeoffFromThreshold` |
| 19 | Lo stesso PIREP respinto e rimandato | rari | già impedito: il volo resta preso dal PIREP respinto (`ClaimedSessionId`) |
| 20 | Rateo di discesa, orario pianificato, apron sbagliato, livello di crociera | rari | giudizio del controllore |

## 4. Le risposte di Carmine (24 settembre 2026)

- **`flightRules`** (nuovo, server, T17): le lettere ammesse (I, V, Y, Z) sono un parametro della regola.
- **`planAtTakeoff`** (nuovo, server, T17): serve un piano valido al decollo, e i controlli sul piano leggono quello. Le revisioni dopo il
  decollo non contano (GR9): si mostrano nell'evidenza.
- **`flightPlanForm`** (nuovo, server, T17), un solo controllo con l'evidenza riga per riga:
  - **REG/** obbligatorio quando il callsign ha la forma di un volo di linea (tre lettere e un numero: `RYR2599`); **non** quando il
    callsign è già la marca dell'aereo (`ICELLO`, `N260MA`). Nei casi dubbi non fallisce: lo segna nell'evidenza.
  - RMK seguito da «/», Z con COM/DAT/NAV nel campo 18, VFR senza DCT e con «VFR» come livello.
  - **SID e STAR nella rotta** solo nei paesi che le chiedono: **impostazione del modulo**, modificabile da coordinator e assistant, che
    parte da `ED` e `LO`. Non in `division.json`: è un fatto dell'AIP, non della divisione.
- **`equipment`** cambia forma: **lettere richieste per ciascuna regola di volo** (nel Turboprop I e Y chiedono S, D, G, R, W, Y; V nessuna).
  Il tour la cambia già, perché ogni tour ha le sue regole. **W** si chiede solo se il piano ha un livello **sopra FL285** (RVSM): RFL o
  un livello nella rotta (`/N0460F390`). Carmine ha citato W **come esempio** («tipo»): T17 apre chiedendo se altre lettere hanno
  una condizione, e il parametro si disegna per più di una.
- **`alternate`**: non passa se manca o se è **uguale alla destinazione**; se è **uguale alla partenza** lo scrive nell'evidenza, senza
  fallire. La regola di `ZZZZ` con `ALTN/` resta.
- **`maxAltitude`** (nuovo, server, **T18**): dalle tracce, parametro nella regola.
- **Livelli volati contro pianificati**: sul **PC del validatore** (T21), perché salite e discese sono pianificate sui fix e serve
  Navigraph.
- **Touch and go e le altre istruzioni di leg**: restano nelle manovre obbligatorie, rimandate.
- **PIREP respinto e rimandato**: già impedito. Manca un test: `PirepTests.cs:222` prova un volo preso da un PIREP in coda, non da uno
  respinto. Lo aggiunge T17.

## 5. I casi di prova

I PIREP di luglio–settembre sono ancora nel tracker IVAO (circa tre mesi): in T17 si registrano come fixture con il token vero, **prima che
spariscano** (quelli di metà settembre fino a metà dicembre). Esiti attesi:

| PIREP | Tour, leg | Atteso |
|---|---|---|
| 879788, 879691, 879558 | Turboprop, 3–5 (B350, piano I) | `equipment` non passa (manca Y); `flightRules` passa |
| 882171 | VFR, 4 (C152) | `flightPlanForm` non passa (DCT nel VFR) |
| 880760 | VFR, 24 | tutto passa; il rifiuto (pista 12/30) è una manovra obbligatoria |
| 881923 | VFR, 22 | `flightPlanForm` non passa: livello `F085` in un piano VFR (**corretto in T17**, Carmine: fallisce; nota `2026-09-24-il-motore-dei-controlli` §2) |
| 877464 | Dangerous Airports, 2 (B737 a London City) | `aircraft`, `speed250`, `parking` non passano |
| 877596 | Dangerous Airports, 4 | `speed250` non passa |
| 877187 | Bizjet, 8 | `speed250` non passa (il pilota dichiara un'emergenza) |
| 877196 | Ryanair Summer, 19 | `simRate` non passa |
| 880159 | Ryanair Summer, 19 | T18: `disconnections` e `landingAtArrival` **passano** (falso positivo del vecchio sistema) |
| 879610, 881263, 881169, 876413 | Volotea, Lufthansa, Ryanair, Itavia | tutto passa (IFR puliti) |

## 6. I punti di Carmine

Nessuno da aggiungere, a occhio. L'unico è quello già detto: le **manovre obbligatorie della leg** (touch and go, pista, VRP), che il
sistema di oggi **non valida** in automatico, si valutano più avanti come cosa a sé, non in T17 né in T18.

## 7. Che cosa non si è verificato

- Le frequenze del §3 vengono da parole chiave, non da una lettura commento per commento.
- «Speed was ok (300 below 6.000 ft)» del vecchio sistema: non si sa che cosa misuri.
- ~~Nessun PIREP del campione è ancora stato registrato come fixture.~~ Registrati in T17 (`tracker-reports-780002.json`): i PIREP distinti
  sono 15, non 16 (nota `2026-09-24-il-motore-dei-controlli` §3).
