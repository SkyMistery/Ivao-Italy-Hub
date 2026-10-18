# Le regole di award, i PIREP automatici, il riepilogo dei validatori (E14b)

**Data:** 17 ottobre 2026 — fase E14b di M4 (M4b), PR #293, in coda dopo la #289 di E14a e la #291 di E10v
**Stato:** le letture del §2 **decise** da Carmine sulla #293 ([i tre punti][a293], [le altre letture][b293], 18 ottobre 2026, in chat al
master e pubblicate su sua istruzione): la 2, 3, 4, 5, 6, 9 e 10 come sono scritte; la 3-bis con **un tempo minimo di volo dalle impostazioni**
e la 8 con **nessuna voce di volo `Auto` per un evento senza scali suoi**, come scritto qui sotto; la 5-bis con **un metodo piccolo del nucleo**,
in una fase del nucleo a sé prima che E14b lo usi. La domanda 1 è **decisa**: Carmine ha scelto la
**(A)** sull'[issue #292][a292] (in chat al master, pubblicata su sua istruzione, 18 ottobre 2026), una riga del modulo per regola
soddisfatta; il punto in più — leggere `cms_award_signals` dal contesto del modulo, o un metodo piccolo del nucleo (lettura 5-bis) — l'ha
lasciato al revisore, quando legge la #293.
La domanda 2 è **decisa**: Carmine ha scelto la **(A)** sulla #293 ([la risposta][c293], in chat al master, «andiamo di A», pubblicata su
sua istruzione, 18 ottobre 2026) — la fase del nucleo **E10v** (#291, `m4/e10v-session-airports`, nota
`2026-10-17-gli-scali-di-una-sessione`), già in `main`, che E14b usa per la metà pilota dei PIREP `Auto`.
**Regola applicata:** `CLAUDE.md` §5. Che un evento abbia regole di award da un **elenco chiuso** di criteri, che accettare un PIREP le
valuti sulle voci **verificate** e proietti un segnale di award per regola soddisfatta **nella stessa transazione**, che chi ha
`Awards.Assign` assegni a mano dalla coda del nucleo; che la preferenza «riporta automaticamente la mia partecipazione agli eventi» (spenta)
faccia scrivere a `events-after` i PIREP `Auto`; che i validatori ricevano una volta al giorno `reportsToValidate`, sono **decisi** (design
`09-design-m4.md` §1.9, §1.11, §5.1, §5.4, §8.3, §8.4; nota `2026-09-29-dopo-l-evento-e-gli-award`, punti 3 e 4; il limite di Carmine
sull'[issue #287][l287]). Questa nota registra le **letture** che la fase ha dovuto fare dove il design non dice — casi **(a)** e **(b)**: due
tabelle nuove (`evt_award_rules`, ed `evt_report_awards` con la (A) della domanda 1) e una colonna (`evt_events.auto_reports_cursor`) nella
migrazione `AddAwardRules` dopo `AddSupportReports` di E14a, un job (`events-digest`), una preferenza, un tipo di mail — e **due domande** dove
quello che il design chiede non entra in quello che il nucleo offre oggi.

[l287]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/287#issuecomment-6234287053
[q292]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/292
[a292]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/292#issuecomment-6246961698
[a293]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/293#issuecomment-6248618376
[b293]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/293#issuecomment-6248633104
[r291]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/291#issuecomment-6246724281
[c293]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/293#issuecomment-6253511103

## 1. Che cosa serviva decidere

Il design dice le colonne di una regola (`event_id`, `award_id`, `kind`, `criterion`, `parameters`) e i sei criteri con i loro parametri;
che accettare valuta le regole e proietta i segnali; che `events-after` crea i PIREP `Auto` per chi ha la preferenza (per un evento
`whole_division` gli aeroporti e le postazioni della divisione); che il riepilogo parte una volta al giorno con il suo «già mandato oggi».
Non dice: come si scrivono i parametri, che cosa vuol dire ogni criterio sulle voci, che cosa succede ai segnali quando la decisione cambia,
come un job a lotti tiene il conto dei membri già guardati, che cosa diventa un PIREP `Auto` di un controllore, a chi e quando parte il
riepilogo.

## 2. Le letture

1. **La tabella `evt_award_rules`** (`EventAwardRule`): un `IEventChild` nell'area **`Events`** dell'evento (§6.1: `Events.Edit` scrive le
   regole), con la cura e lo scope dell'evento, `[Audited]`, chiave verso l'evento in cascata (come le rotte), al più **50 per evento**. Un
   evento **in presenza** non ne ha: non prende PIREP (`awardRuleInPerson`).
2. **I parametri sono colonne, non un `parameters` JSON**: `departure_icao`, `arrival_icao`, `count`, `distance_nm`, `minutes`,
   `position_type`, ognuna letta dai criteri che la prendono e **svuotata dal server** per gli altri. `kind` lo scrive il server dal criterio
   (`AnyFlight`, `MinFlights`, `Route`, `RouteTimes`, `MinLegDistance` leggono un PIREP pilota; `AnyShift`, `MinAtcMinutes` uno ATC). I
   limiti: un award **attivo** del catalogo; gli aeroporti di una rotta noti al nucleo e diversi; `count` da 1 a 20 (le voci di un PIREP);
   `distance_nm` da 1 a 10800; `minutes` da 1 a 1440; `position_type` uno dei tipi delle postazioni della divisione, o nessuno. Una colonna
   per parametro si legge, si ordina e si controlla con le regole di sempre; il JSON avrebbe chiesto un validatore a mano.
3. **Che cosa vuol dire ogni criterio**, sulle voci **con `verified` vero** (una voce non trovata, o non chiesta, non è un volo volato né un
   turno controllato): `AnyFlight`/`AnyShift` almeno una; `MinFlights` almeno `count`; `Route` una da `departure` ad `arrival` (il verso
   conta); `RouteTimes` almeno `count` di quella rotta; `MinLegDistance` una con la distanza fra i due aeroporti (quella che la verifica ha
   scritto, `GreatCircle`) **almeno** `distance_nm` — «più lunga di x» del design letto come «di almeno x», come dice il nome; una distanza non
   nota non basta; `MinAtcMinutes` la **somma** dei minuti delle voci, solo sulle postazioni del tipo scelto quando c'è (il tipo di una
   postazione dal nucleo, `IAtcPositionDirectory`; un nominativo che il nucleo non conosce non ha tipo).
3-bis. **Quale rotta conta: sempre l'ultima del piano, mai una solo depositata** (il punto 2 del revisore sulla [#291][r291]). La rete non
   dice dove un volo è atterrato: dice i piani che il pilota ha depositato, revisione dopo revisione (`Routes` di E10v), e l'**ultima** è quella
   che volava alla fine (`DepartureIcao`/`ArrivalIcao`). Un pilota che deposita LIRF→LICR, corregge in LIRF→LIMC e vola quella ha tutte e due
   in `Routes`, ma ha volato solo la seconda. Quindi:
   - **una voce di volo è verificata solo quando la sessione trovata ha come ultima rotta quella della voce** — per un PIREP mandato a mano la
     verifica (`ReportCheck`) scarta le sessioni che la rete trova per una revisione precedente; per un PIREP `Auto` la voce **è** l'ultima rotta
     della sessione (lettura 8). Le regole leggono solo le voci verificate (lettura 3), quindi:
   - `Route`: una voce verificata con quella partenza e quell'arrivo — l'ultima rotta di una sessione;
   - `RouteTimes`: le voci verificate di quella rotta, **una per sessione** (due revisioni uguali sono una tratta sola);
   - `MinLegDistance`: la distanza fra i due aeroporti di una voce verificata, cioè dell'ultima rotta;
   - `AnyFlight`, `MinFlights`: le voci verificate, cioè le sessioni con l'ultima rotta dichiarata;
   - i PIREP `Auto`: una voce per sessione con la sua ultima rotta; le revisioni precedenti non fanno voci.

   **E dura almeno il tempo minimo di un volo** ([la risposta 1 di Carmine][a293]): l'impostazione nuova **`minimumFlightMinutes`** (10 di
   predefinito, da 1 a 120, nel form delle impostazioni): una voce di volo è verificata solo se la sua sessione è durata almeno tanto, per un
   PIREP mandato a mano e per uno `Auto` (che sotto la soglia scrive la voce, non verificata); sotto, nessuna regola è soddisfatta — una
   connessione di due minuti con un piano LIRF→KJFK non vale `MinLegDistance` (`ReportRules.LongEnough`, il confine provato da un test).

   ⚠️ **Il prezzo**: un pilota che in una connessione sola vola una tratta e poi deposita la successiva (la sessione `1000007` della sera di
   Fiumicino: LIPZ→LIRF, poi LIRF→LICR) ha verificata solo la seconda; la prima resta «non trovata», e chi valida la vede così. Scartato:
   contare ogni revisione di `Routes`, che darebbe un award per una rotta solo depositata.
4. **I segnali si calcolano all'accettazione** e restano come sono: cambiare o eliminare una regola dopo non tocca i segnali dei PIREP già
   accettati (si rivaluta solo una nuova accettazione). Il motivo del segnale è nella lingua della divisione: l'evento e il criterio.
5. **Una decisione che cambia** (la scelta 1 di E14a e il limite di Carmine sulla #287): **una riga per PIREP e regola, mai cancellata**
   (univoco `(report_id, rule_id)`; il punto 1 della revisione della #293): da accettato a rifiutato le righe restano e **smettono di
   segnalare** (`points_out` falso, la proiezione vuota): i segnali ancora **in attesa** se ne vanno (il nucleo toglie un `Pending` che la riga
   non proietta più), uno **scartato** da chi assegna resta com'è nella coda. Da rifiutato ad accettato le regole si rivalutano: una regola
   soddisfatta di nuovo ritrova **la sua riga e il suo segnale** — uno scartato resta scartato, nessun segnale nuovo accanto al vecchio —, una
   che non lo è più smette di segnalare. Il motivo e l'award di una riga sono quelli della prima volta. Se **un segnale è diventato un award**
   (`Handled`) — anche uno scartato, poi riaperto e assegnato da chi assegna dopo un rifiuto, che il nucleo permette — il cambio è
   **rifiutato** con `reportAwardAssigned`, e la scheda non offre più «Cambia la decisione» (`Decidable` falso): la domanda guarda **ogni
   riga** del PIREP, quindi ogni segnale che ha mai dato.
5-bis. ⚠️ **Per il revisore: il modulo legge una tabella del nucleo.** La domanda «è diventato un award?» il modulo la fa a
   **`cms_award_signals`** (lo stato `Handled`) **dal contesto degli eventi**, che mappa la tabella con la configurazione del nucleo
   (`ModuleDbContext.MapProjectionTables`, esclusa dalle sue migrazioni: c'è perché l'interceptor scriva i segnali nella transazione della
   riga), e la legge **sotto `FOR UPDATE`** delle righe dei segnali, nella transazione della decisione: un'assegnazione nello stesso istante
   passa prima o dopo, mai in mezzo (`ReportAwards.AssignedAsync`); la lista dello staff la legge senza blocco
   (`ReportAwards.AssignedAmongAsync`). **Il precedente** è `TourRetentionJob` dei tour, che legge la stessa tabella (dal contesto del nucleo,
   senza blocco) per sapere se un segnale aspetta ancora. Nessun servizio del nucleo risponde oggi a «una riga sorgente ha dato un award?»; se
   il revisore la vuole letta attraverso il nucleo, è una fase del nucleo a sé (un metodo che risponda per `source_module` + `source_id`,
   sotto il blocco quando la chiamata è in una transazione), e il modulo lo chiamerebbe al posto delle due query. **Decisa** ([la risposta 3
   di Carmine][a293]): **il metodo del nucleo**, in una PR del nucleo a sé con la sua nota, caso (b), prima che E14b lo usi; E14b ne unisce il
   branch e lo chiama al posto delle sue due query su `cms_award_signals`. **Fatto**: la fase **E10x** (#299, nota
   `2026-10-18-un-segnale-diventato-award`) dà `AwardSignalQueries.AssignedAsync(context, sourceModule, sourceIds)`; `ReportAwards` la chiama
   sulle sorgenti di **ogni** riga del PIREP, nella transazione della decisione (il blocco `FOR UPDATE` lo prende il nucleo) e senza blocco
   nella lista dello staff.
6. **«Duplica» copia le regole** (design §2.3-bis: «ogni blocco copia le righe che esistono nella sua fase»), come E11a ha fatto per le
   postazioni ed E17 per domande e attività: l'award, il criterio e i parametri come sono, nella cura della copia, senza ricontrollarli (un
   award ritirato si rifiuta al prossimo salvataggio della regola). Le regole sono dell'area `Events`: chi duplica non ha bisogno di un
   permesso in più. ⚠️ La riga di E15b «Duplica copia anche postazioni e regole di award» è già fatta: le postazioni da E11a, le regole qui.
7. **Le statistiche** (`award_signals` di `evt_event_stats`, a 0 da E13a): le righe dell'evento che segnalano quando le statistiche si
   scrivono (`points_out`), cioè le regole soddisfatte dai PIREP accettati.
8. **I PIREP `Auto`** (§5.1): un passo di `events-after` **dopo i turni e prima della chiusura**, con il suo peso nel lotto (**2 unità per
   membro**: i suoi voli, i suoi turni). I membri con la preferenza accesa, **nell'ordine dei VID**; quello fatto per ultimo si scrive
   sull'evento (`auto_reports_cursor`, il segno del job come `after_done_at`: non muove la versione della riga né il suo audit; un cursore, non
   una colonna di persona, quindi non si chiama `…Vid` e la cancellazione dei dati di una persona non lo tocca), così il giro dopo riprende da
   lì; l'evento si chiude quando sono fatti tutti. Mai dopo i trenta giorni (le sorgenti abbandonate: chi vuole manda il suo a mano), mai per
   un evento in presenza; **nessun limite di `reportDays`** (lo scrive il sistema). Un PIREP che c'è già — del membro, o scritto prima — **non
   si tocca** (lo stesso indice univoco); un `Auto` che il membro corregge diventa suo (`Manual`). Un `Auto` non manda mail al membro: aspetta
   chi valida, nel riepilogo.
   - **ATC**: le sessioni del membro come controllore **dal tracker** (`INetworkSessionSource`, inizio fino a 12 ore prima della finestra degli
     slot, come un turno di E13a) sulle postazioni dell'evento **e** su quelle della divisione ai suoi scali, o su **tutte** quelle della
     divisione per un evento `whole_division`; **una voce per postazione**, dal primo minuto all'ultimo dentro la finestra, al più dodici ore,
     al più venti voci; i minuti come li conta E13a; **verificata con la soglia di un turno verificato a mano e di uno del roster**
     (`AfterRules.Threshold`: `attendanceMinimumMinutes`, al più tutta la voce), una regola in un posto solo (un nit della revisione della #293).
   - **Pilota**: sulle sessioni di E10v (la domanda 2), una domanda per VID, tipo pilota, nella finestra degli slot. **Una voce per sessione,
     con l'ultima rotta del suo piano** (`DepartureIcao`/`ArrivalIcao` del nucleo, l'ultima revisione): la sessione è dell'evento quando
     quella rotta tocca uno dei suoi scali, un aeroporto della divisione per un evento `whole_division` (l'aria della divisione del nucleo,
     `IFirDirectory`); **un evento senza scali suoi, non di tutta la divisione, non scrive voci di volo `Auto`** ([la risposta 2 di
     Carmine][a293]: niente dice quali voli sono suoi; i suoi membri mandano il loro PIREP a mano, e chi valida decide); una sessione senza
     piano, o da un aeroporto a sé stesso, non è un volo. La voce è verificata quando la sessione è durata almeno `minimumFlightMinutes`
     (lettura 3-bis), con i minuti della sessione e la distanza fra i due aeroporti quando l'hub li conosce; al più venti voci. **Le regole leggono quella voce**: `Route` e `RouteTimes` l'ultima rotta, `RouteTimes` conta le sessioni.
     Scartato: una voce per ogni revisione di `Routes` (una revisione depositata non è una tratta volata: un pilota che corregge il piano
     avrebbe due voci per un volo solo).
9. **`events-digest`** (§5.3, §8.4): ogni giorno alle **07:15 nel fuso della divisione** (il trigger lo dice), dopo il riepilogo dei tour delle
   07:00. A chi tiene `EventReports.Edit` (`IPermissionHolders` del nucleo), i PIREP `Submitted` degli eventi che **lui** può validare
   (dipartimento e scope come l'unico handler), **i suoi esclusi**, una riga per evento con quanti e da quando, e il link alla scheda; nessuna
   mail a chi non ha niente. **«Già mandato oggi» è la riga del giro** (E10j): un giro che trova un giro riuscito dello stesso giorno della
   divisione non manda niente e lo scrive nella sua riga. Le mail e la fine del giro in un commit solo, come il riepilogo dei tour.
10. **La preferenza** `events.autoReport` (`IModule.Preferences`, un booleano; nessuna riga è spenta): un interruttore in cima alla sezione
    «PIREP di supporto» di `/events/mine`, che ora si vede sempre — anche a chi non ha niente da mandare —, perché la preferenza vale per gli
    eventi che verranno.
11. **La mail** `events.reportsToValidate`, spegnibile dal profilo come ogni mail del modulo.

## 3. Le alternative scartate

| Alternativa | Perché no |
|---|---|
| I parametri in una colonna JSON | un validatore a mano, nessuna lettura per colonna; sei parametri stanno in sei colonne |
| Il criterio come espressione libera | il design: «un criterio nuovo è una riga dell'elenco nel codice» |
| Ricalcolare i segnali quando una regola cambia | un PIREP accettato resta come chi l'ha accettato l'ha visto; chi assegna ha già una coda |
| Un PIREP `Auto` riscritto a ogni giro | il membro o chi valida lo leggono; un giro dopo non deve cambiarlo sotto di loro |
| Il «già mandato oggi» in una tabella | E10j: è la riga del giro |
| Ripetere la verifica del membro (`ReportCheck`) per le voci `Auto` | le sessioni sono già quelle trovate: una seconda domanda per voce costerebbe il lotto |

## 4. Le domande — decise da Carmine, (A) tutte e due

1. **Un PIREP non può proiettare due segnali per il suo membro.** (Pubblicata sull'[issue #292][q292].) Il nucleo tiene un segnale **per membro per riga sorgente** (l'indice univoco
   `(source_module, source_id, vid)` di `cms_award_signals`, e `ProjectionWriter` riconosce un segnale dal VID). Un PIREP è di un membro: con
   una regola soddisfatta va bene, con **due** il secondo segnale urta l'indice (l'accettazione cade) o, a un'altra proiezione, prende l'award
   del primo.
   - (A) **Una riga del modulo per regola soddisfatta** (`evt_report_awards`: il PIREP, la regola, l'evento, il VID del membro, l'award),
     scritta dall'accettazione nello stesso salvataggio; è lei l'`IProjectable`, con il suo segnale (`report-award:{id}`). Rifiutare la
     cancella, e il nucleo toglie il segnale in attesa. Niente del nucleo cambia; una tabella in più nella stessa migrazione.
   - (B) **Prima una fase del nucleo**: la chiave del segnale diventa `(source, id, vid, award)` — l'indice e il confronto del writer — e il
     comportamento dei tour cambia: «l'award del tour è cambiato, il segnale in attesa propone il nuovo» (T4b) diventerebbe «tolto, e uno
     nuovo», con una mail nuova a chi assegna.
   - (C) **Un segnale per PIREP** che nomina tutte le regole soddisfatte, con l'award della prima; gli altri award si assegnano a mano senza
     segnale.

   **Raccomandata: (A)**: usa il meccanismo com'è (una riga sorgente per segnale, come l'iscrizione a un tour), non tocca i tour, e ogni award
   ha il suo segnale in coda. Il codice della PR la segue, con la lettura 5-bis per sapere se un segnale è diventato un award; con la (B) o la
   (C) i segnali si riscrivono, e la (B) passa prima da una fase del nucleo. **Decisa: (A)**, da Carmine ([la risposta][a292]); la lettura
   5-bis la guarda il revisore.
2. **I PIREP pilota `Auto` hanno bisogno degli aeroporti di una sessione, che il nucleo non dà.** `INetworkSessionSource` (E10t) risponde
   `NetworkSession(Id, Vid, Callsign, StartedAt, EndedAt)`: il DTO del tracker dietro ha partenza e arrivo, ma il record neutro li lascia.
   Una voce pilota è callsign, partenza, arrivo e giorno, e tre criteri leggono gli aeroporti; chiedere per aeroporto non basta (con la
   partenza per VID si sa la partenza, mai l'arrivo), e un evento `whole_division` chiederebbe due domande per ogni aeroporto della divisione
   per ogni membro. Gli aeroporti della divisione il nucleo li ha (`IFirDirectory`), e le postazioni (`IAtcPositionDirectory`): i PIREP ATC
   `Auto` si fanno già.
   - (A) **Una piccola fase del nucleo prima**: `NetworkSession` prende `DepartureIcao` e `ArrivalIcao` facoltativi (gli aeroporti del piano
     di volo, vuoti per un controllore o senza piano), riempiti da `NetworkSessionSource` dal DTO che legge già; E14b ne unisce il branch e
     scrive i voli `Auto`: una domanda per membro, i voli che toccano gli scali dell'evento, o un aeroporto della divisione per un evento
     `whole_division`, verificati come li ha trovati.
   - (B) **Niente nel nucleo**: E14b scrive solo i PIREP ATC `Auto`, e i voli aspettano una fase dopo.

   **Raccomandata: (A)**: due campi facoltativi di un record del nucleo, letti da un DTO che già li ha. Il codice la segue: dalberone ha
   scelto la strada, la fase del nucleo **E10v** (#291), per la regola 6. **Decisa: (A)**, da Carmine ([la risposta][c293], in chat al
   master il 18 ottobre 2026, «andiamo di A», pubblicata su sua istruzione): la piccola fase del nucleo prima, E10v, già unita, ed E14b la
   usa.

## Da portare nel piano

§9.2 riga Events (le regole di award, i segnali per regola, il rifiuto dopo un award assegnato, i PIREP `Auto`, il riepilogo); il design
`09-design-m4.md` §1.9 (i parametri come colonne, i limiti, nessuna regola su un evento in presenza), §1.10 (`award_signals`), §1.11 (dove
sta l'interruttore), §2.3-bis (le regole copiate), §5.1 (il passo dei PIREP `Auto` e il suo segno), §5.4 (che cosa vuol dire ogni criterio, i
segnali che restano, la decisione che non cambia più), §8.4 (`events-digest` alle 07:15 nel fuso della divisione); il piano di M4, E15b
(«Duplica» per le regole è fatto).
