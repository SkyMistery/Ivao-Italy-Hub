# Gli ATC contattati e le esenzioni (T12)

**Data:** 23 settembre 2026 — fase T12 di M2
**Stato:** **decisa** (Carmine, 23 settembre 2026, tre risposte in apertura; il resto è design già scritto o scelta tecnica
dichiarata qui sotto)
**Regola applicata:** `CLAUDE.md` §5. Il meccanismo nuovo del nucleo (`IAtcActivitySource`) era già deciso: piano 0.78, nota
`2026-09-14-dati-condivisi-con-vipi.md`, design M2 §6.5. Qui ci sono **tre precisazioni del design** (caso c, decise da Carmine) e
**un'estensione di meccanismo** (caso b): il contesto EF di sola lettura su una vista di un altro sito.

## 1. Che cosa serve

Design M2 §3.3: mentre il pilota compila il PIREP, il sistema **propone** gli ATC che erano online lungo il volo (versione leggera,
sul server: aeroporti di partenza, arrivo e deviazione, e FIR attraversati dai punti delle tracce), il pilota **toglie** e
**aggiunge**, e dichiara le **esenzioni** ricevute; ogni tipo di esenzione dice quali controlli ammorbidisce. Il design lasciava
aperte tre cose.

## 2. Le tre risposte di Carmine

1. **Il perimetro delle esenzioni** (Toursystem ADR-014: nessuna esenzione generica): `FreeSpeed` → `speed250`; `LevelChange` →
   `semicircularLevels`; `DirectRouting` → **nessun controllo di M2** (nessuno legge la rotta: ammorbidirà l'aderenza alla rotta
   quando ci sarà); `Other` → nessuno, è testo per il validatore e **la nota è obbligatoria**. Scartato: togliere `Other` (chi ha
   un'esenzione diversa l'avrebbe scritta nelle note del PIREP, fuori dalla lista).
2. **Tre stati per un'esenzione**, non due: `Online` (l'archivio la vede online durante il volo), `NotOnline` (l'archivio copre
   **tutto** l'intervallo e non la elenca) e `Unverifiable` (nessun archivio, o un archivio che non arriva così indietro). **Non
   blocca mai l'invio**: un callsign scritto male o un buco dell'archivio non devono fermare un pilota; il validatore vede lo stato.
   Scartato: rifiutare l'esenzione sotto il campo.
3. **Gli ATC proposti e tolti restano scritti**, con origine `Removed`: il validatore vede chi era online e che il pilota dice di
   non aver contattato. Le origini sono tre: `Proposed` (proposto e tenuto), `Added`, `Removed`. Scartato: tenere solo i tenuti,
   come diceva il design alla lettera.

## 3. Le scelte tecniche (dichiarate, non domandate)

- **La proposta si rifà al momento dell'invio**, sul server, con le stesse tracce che l'invio già legge: un `proposed` scritto dal
  browser non prova niente. Il browser manda solo i callsign che il pilota tiene o aggiunge; il server li confronta con la sua
  proposta e scrive le tre origini.
- **Le finestre**: la partenza conta dall'inizio della sessione a **20 minuti dopo il decollo** (APP, DEP); l'arrivo e la
  deviazione da **30 minuti prima dell'atterraggio** alla fine della sessione; un FIR conta **nell'istante** in cui la traccia ci
  stava, un punto in volo al minuto. La posizione si riconosce da ciò che precede il primo `_` del callsign (`LIRF_TWR` → `LIRF`,
  `LIRR_N_CTR` → `LIRR`). Costanti in `AtcProposal` e `AtcProposer`, tarabili sui voli veri.
- **Da quando l'archivio è completo** non si scrive da nessuna parte: si chiede la riga più vecchia di ciascuna metà della vista
  (`MIN(start_utc)` per `is_outside_division`, in cache un'ora). Così la potatura a dodici mesi di vIPI e l'archivio mondiale dal
  28 agosto 2026 cambiano la risposta da soli. Una posizione è «della divisione» se il suo prefisso è in `division.json →
  icaoPrefixes`.
- **Una colonna in più**, `fo_pireps.atc_archive_available` (migrazione additiva `AddAtcArchiveAvailable`): senza, una lista vuota
  di ATC non distingueva «nessuno era online» da «non lo sappiamo». I due JSON (`atc_contacts_json`, `atc_exemptions_json`)
  esistevano già da T11a; l'esenzione congela anche i controlli che ammorbidisce, come le regole congelano i loro parametri (§5.4).
- **La query sulla vista** cerca le sessioni iniziate fino a **due giorni** prima dell'intervallo, così cammina sull'indice
  `(IsOutsideDivision, StartUtc)` di vIPI invece che su tutto l'archivio. Una connessione aperta da più di due giorni non si vede.
- **L'attribuzione dei confini** (CC BY-SA 4.0) arriva al form da `IFirLocator.Attribution`: il modulo la mostra accanto alla
  proposta senza sapere chi ha disegnato i poligoni.
- **Il form**: una sezione «ATC contattati» fra il volo e i dettagli — le caselle dei proposti, e un `SchemaForm` che si applica
  mentre si scrive per gli ATC aggiunti e per le esenzioni (la posizione di un'esenzione si sceglie fra i contattati: lo schema
  si costruisce con loro). Un invio fatto prima che la proposta arrivi la aspetta. I rifiuti su `atcContacts` ed `exemptions`
  compaiono sotto la sezione.

## 4. L'estensione del meccanismo

Il test di architettura vuole che un contesto EF si registri solo dai metodi che attaccano l'interceptor. Il contesto della vista
di vIPI **non scrive mai** (il suo `SaveChanges` lancia un'eccezione), non ha migrazioni né interceptor e usa una connessione sua:
`AddSharedViewContext<TContext>(nomeConnessione)`, **accanto** agli altri due in `HubDbContextServiceCollectionExtensions`, perché
l'unico posto dove si costruisce un contesto resti uno. Il prossimo sito che condivide una vista (vIPI che legge lo staff
dell'hub, nota del 14 settembre §3.5, è dall'altra parte) usa lo stesso metodo.

## 5. Che cosa si tocca

- **Nucleo**: `Core/Atc/` (`IAtcActivitySource`, `AtcActivity`, `AtcPresence`, `UnavailableAtcActivitySource`,
  `VipiAtcActivitySource` con `VipiShareDbContext`, `AddAtcActivity`, `AtcDataOptions`); `DivisionOptions.AtcData` con il suo
  controllo all'avvio; `IFirLocator.Attribution`; `AddSharedViewContext`.
- **Modulo**: `Pireps/AtcContacts.cs` (pura), `Pireps/AtcProposer.cs`, `GET …/reports/atc`, l'invio e la lettura del PIREP.
- **Test di architettura**: nessun modulo nomina vIPI, `v_share`, VATSpy od OpenAIP (server e browser).
- **Configurazione**: `atcData` in `division.example.json` (`none`); `division.json` **resta senza** finché il server non è pronto.
- **vIPI**: la migrazione della vista, in un branch e una PR del repository di vIPI (Carmine, 23 settembre: «la scrivo io»).

## 6. Prerequisiti fuori da questo repository, ancora aperti

- **Le due verifiche sul server** della nota del 14 settembre §4 — l'utente MariaDB dell'hub con solo `SELECT` sulle viste
  `v_share_` di `itivao_atc`, e il privilegio dell'utente di vIPI di creare viste `SQL SECURITY DEFINER` — le fa Carmine dal
  pannello. Finché mancano, `division.json` non ha `atcData` e l'hub dice «non disponibile».
- **Accenderla** vuol dire: la release di vIPI con la vista, il `GRANT`, `ConnectionStrings:AtcData` in un file sotto `secrets/`,
  e `"atcData": { "source": "vipi" }` in `division.json`.
