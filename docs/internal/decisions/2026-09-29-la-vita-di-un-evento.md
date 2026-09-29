# La vita di un evento: l'uscita programmata, la fine, «Duplica»

**Data:** 29 settembre 2026 — fase E0 di M4
**Stato:** **decisa** (Carmine, 29 settembre 2026, sulla PR #180: [conferma di §17.1 e §17.2][ok] e [conferma di §17.3][ok3];
§17.1 n.14, §17.2 n.4, §17.3 n.4, come raccomandato).
**Regola applicata:** `CLAUDE.md` §2 (le proiezioni con `IProjectable`, nella stessa transazione; gli usi dei file con scadenza) e
§5, caso **(b)** per l'uscita programmata e la fine, **(c)** per «Duplica». Design `09-design-m4.md` §2, §8.1, §8.4, §17.1 n.14,
§17.2 n.4, §17.3 n.4; nota `2026-09-09-il-documento-dice-di-se` §2.

[ok]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/180#issuecomment-5880522987
[ok3]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/180#issuecomment-5880732900

## 1. Che cosa serviva decidere

- **L'uscita programmata** di un evento (nota `il-documento-dice-di-se` §2, che la prevedeva e poneva tre domande): che cosa esce,
  che cosa succede a un rifiuto all'ora X, e il contrario, sparire a una data.
- **La fine** (c2): lo staff dell'ED vuole che per il pubblico l'evento **sparisca quando termina**, con le statistiche e le
  informazioni che restano allo staff, e i banner eliminati **7 giorni dopo la fine**, per risparmiare spazio.
- **Gli eventi che si ripetono** (RFE, Online Day) e gli errori che nascono a rifarli a mano (§17.3 n.4).

## 2. Le decisioni

1. **Lo stato si calcola dalle date** a ogni richiesta, come per i tour (design §2.1): bozza, programmato, annunciato, prenotazioni
   aperte, in corso, concluso, annullato. **Nessun job cambia uno stato.**
2. **L'uscita programmata ha la forma dei tour** (§17.2 n.4): la riga è **pubblicata** e **`visible_from_utc`** dice da quando si
   vede. Alle tre domande della nota: esce la riga **com'è**, perché è già passata dai controlli di «Pubblica»; il rifiuto **non può
   capitare** all'ora X; il contrario è la fine dell'evento. La proiezione segue con il job **`events-release`**, che decide
   dall'ultimo giro riuscito come `TourReleaseJob`. **Tour ed eventi hanno la stessa forma: passa nel nucleo al terzo modulo** che
   la chiede, non ora.
3. **La fine** (§17.1 n.14): dopo `ends_at_utc` **per il pubblico l'evento non esiste più** — la pagina risponde 404 a chi non è
   staff, la lista e il blocco lo tolgono, e anche **il calendario e la ricerca** (la proiezione di un evento concluso è vuota, e
   `events-release` riproietta) —; il membro vede le sue righe in `/events/mine`; lo staff ha tutto. **Nessun archivio pubblico.**
   Un evento annullato resta visibile con la nota fino alla sua fine, senza voce di calendario.
4. **I banner e le immagini della descrizione** sono dichiarati come **usi dei file fino alla fine + 7 giorni**
   (`MediaUseProjection`): il job del nucleo li elimina se nessun altro li usa.
5. **«Duplica»** (§17.3 n.4, `Events.Edit`): una **nuova data d'inizio** e un nuovo slug; l'hub crea **una bozza** con tutti gli
   orari spostati della stessa differenza e copia titolo, descrizione e banner, scali con la capacità, rotte, regole di award,
   postazioni ATC e loro finestre, domande e attività e, **a scelta**, gli slot pubblici con le rotazioni. **Non copia niente dei
   membri** (prenotazioni, disponibilità, turni, iscrizioni, PIREP); i privati si rigenerano. Un solo endpoint, una transazione;
   ogni blocco copia le righe che esistono nella sua fase.
6. **Annullare** (`Events.Edit`) scrive `cancelled_at` e una nota tradotta e avvisa chi ha prenotato o ha un turno; **eliminare**
   (`Events.Delete`, solo EC ed EAC) si può solo senza righe dei membri, altrimenti si annulla.

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Un job che «pubblica» all'ora X | il rifiuto potrebbe capitare quando nessuno guarda; la riga pubblicata con una data non ha questo problema |
| L'uscita programmata nel nucleo ora | due moduli con la stessa forma non bastano a dire quale sia il meccanismo; al terzo sì (§17.2 n.4) |
| L'evento concluso consultabile in sola lettura (come `ivao-booking`) | c2: per il pubblico sparisce; resta allo staff e al membro per le sue righe |
| Un job che cancella i banner | c'è già il job del nucleo sugli usi dei file con scadenza |
| «Duplica» che copia anche le prenotazioni | sono dei membri, e l'evento nuovo è un altro evento |

## 4. Che cosa si tocca, e dove

**E3a** annullare ed eliminare con `DeletePolicy`; **E3b** pubblicare, l'uscita programmata, la fine, il calendario, la ricerca, gli
usi dei file, `events-release`; **E4** il 404 al pubblico e la lista senza i conclusi; **E8b** «Duplica» per quello che M4a ha;
**E15b** e **E17** «Duplica» per le righe di M4b e di M4c.

## Da portare nel piano

**Già nel piano 1.24**: §9.5 (bozza, non ancora visibile, annullato o concluso: nessuna voce), §8.2 (l'evento concluso sparisce dal
pubblico), e nel changelog la risposta alle tre domande della nota `il-documento-dice-di-se` §2, che resta com'è. Resta:

- **§9.5** (dove il corpo del piano dice dei tour rilasciati con `TourReleaseJob`): l'uscita programmata ha ora **due** moduli con la
  stessa forma, e **al terzo passa nel nucleo** (§17.2 n.4), con la sua nota. Oggi la regola sta solo nel changelog 1.24.
