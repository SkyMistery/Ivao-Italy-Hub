# I tour nel back office: il rilascio senza scrittura, T6 in due, gli aerei consentiti senza «varianti»

**Data:** 16 settembre 2026 — fase T6a di M2
**Stato:** **decisa** (Carmine, 16 settembre 2026, tre domande in apertura di T6; il resto sono estensioni di meccanismi esistenti,
dette qui perché toccano il nucleo).
**Regola applicata:** `CLAUDE.md` §5: **(c)** per il job del rilascio e per gli aerei consentiti (una regola del design cambia),
**(b)** per `ProjectionRefresh`, l'orologio delle proiezioni, `CrudOptions.DeletePolicy` e le funzioni SQL dei contesti dei moduli
(si estendono meccanismi, non si aggirano).

## 1. Che cosa serviva

Aprendo T6, tre bivi che il design non chiudeva:

1. **Un tour pronto con il rilascio nel futuro e senza anteprima** è dello staff; al rilascio diventa di tutti **senza che nessuno lo
   salvi** (design §1.2.1: «nessun job pubblica o chiude un tour»). Ma ricerca e calendario si scrivono solo quando la riga si salva:
   resterebbero «staff» per sempre.
2. **La fase era grande**, e il briefing chiede l'editor dei blocchi, che oggi è cucito dentro l'editor dei contenuti (1100 righe).
3. **Gli aerei consentiti** nascono come colonna in T6. La spunta «anche le varianti» del design §1.5 presupponeva che le varianti
   IVAO fossero i tipi imparentati (A20N per A320); misurato in T1, sono livree e motori dello stesso tipo (`A320w`, `A320CFM`).

## 2. Le decisioni

1. **Un job del modulo riproietta i tour rilasciati** (Carmine). `TourReleaseJob`, ogni quarto d'ora, prende i tour pronti, non
   nascosti, senza anteprima, con il rilascio fra l'inizio dell'ultimo giro riuscito e adesso, e li **proietta di nuovo senza
   scriverli**. Lo stato resta calcolato dalle date: il job non pubblica niente, aggiorna solo lo specchio. *Scartati*: ricerca e
   calendario pubblici appena il tour è pronto (il titolo di un tour in arrivo senza anteprima si vedrebbe prima del rilascio, contro
   §1.2.1); «pronto» con rilascio futuro solo con l'anteprima (facile da dimenticare, e toglie una scelta al FOD).
2. **T6 si divide** (Carmine): **T6a** è tutto il server, la lista, il form delle impostazioni, banner e foto, la barra delle azioni e
   i test; **T6b**, in una chat nuova, estrae l'editor del corpo dall'editor dei contenuti e lo monta come scheda «briefing». Il
   «fatta quando» di T6 lo chiude T6a; il server accetta già il briefing (envelope validato, testo in ricerca, immagini come usi).
3. **Aerei consentiti = tipi ICAO più gruppi di aerei, senza la spunta** (Carmine). Chi vuole A320 e A20N insieme usa un gruppo, come
   T5 già permette. La colonna `allowed_aircraft_json` nasce in T6 con la forma `{ types, groupIds }`; il form e il controllo sono di T7.

## 3. Che cosa si estende nel nucleo, e perché

- **`ProjectionRefresh`** (`Core/Content`): chiede all'interceptor di proiettare di nuovo delle righe **al prossimo salvataggio del loro
  contesto**, anche se nessuna è cambiata. Passa dall'interceptor e non intorno: stessa regola della bozza, stessa transazione, nessuna
  riga d'audit, nessuna nuova `row_version` sotto un editor aperto. Il commento di `ProjectionWriter` («mai da un job») resta vero: il
  job chiede, l'interceptor scrive. *Scartato*: segnare la riga modificata (`State = Modified`) — riga d'audit vuota e conflitto con chi
  la sta modificando.
- **L'orologio in `ProjectionContext`**: un tour è dello staff fino al rilascio e di tutti dopo, quindi la proiezione deve sapere che ore
  sono. È l'`IClock` dell'host, così un test che sposta il tempo sposta anche questo.
- **`CrudOptions.DeletePolicy`**: una policy che l'eliminazione chiede **in più** di quella di scrittura. `Tours.Edit` e `Tours.Delete`
  sono di persone diverse (design §7.2: l'advisor modifica e non elimina); prima il motore chiedeva la stessa policy per le due cose.
- **Le funzioni SQL nei contesti dei moduli**: `ModuleDbContext` registra `LocalizedQuery` e `JsonQuery` come `HubDbContext`. Senza, la
  ricerca su un campo tradotto di una lista di modulo rispondeva 500: i gruppi di aerei di T5 ne avevano una e nessun test la cercava;
  l'ha trovata il test dei tour.

## 4. Le scelte piccole, dette qui

- **«Pronto» è `PublishStatus.Published`**: il design dice `Draft`/`Ready`, ma `Ready` nel nucleo vuol dire «in attesa di approvazione»
  e la regola «una bozza tiene solo i suoi file» è già dell'interceptor per ogni `IPublishable`. Nell'interfaccia resta «pronto».
- **`visibility` è grossolana**: `Public` per un tour pronto, non nascosto, non template; `Staff` altrimenti. Una colonna non segue
  l'orologio: la lettura pubblica (T10) chiede anche `TourState.IsPublic`, che aggiunge il rilascio.
- **Torna in bozza solo prima del rilascio**; dopo, si nasconde. **La chiusura a due finestre da oggi** vale per un tour pronto; una
  bozza la sposta liberamente. **Un tour pronto resta pronto solo se potrebbe esserlo**: ogni salvataggio ripassa i controlli.
- **«Ha PIREP?»** è `ITourReports`: risponde no finché T11 non lo sostituisce. Il test la prova sostituendo la risposta, non con una
  riga scritta a mano (la tabella non esiste ancora).
- **Il template copia** tipo, progressione, finestra, limite, procedure, aerei, aereo di riferimento, ordine delle rotazioni, rating
  minimo, obiettivo e distanza, briefing (con identificatori nuovi) e foto; **mai** indirizzo, date, award, stato, anteprima. Titolo e
  indirizzo del tour nuovo li chiede la schermata «Nuovo da template».
- **Il rifiuto di spegnere il limite** elenca i tour con la stessa espressione del validatore (`TourState.NeedsOwnDailyLimit`), come
  filtro della lista dei tour: la schermata delle impostazioni li mostra sotto il form.

## 5. Che cosa si tocca

Piano 0.84 (§9.7 `ProjectionContext`, §16.6 `DeletePolicy`, §16 proiezioni); design M2 §1.2, §1.2.1, §1.5, §14; `06` parte C (T6a e T6b).
Codice: `Core/Content/ProjectionRefresh.cs`, `Projections.cs`, `HubSaveChangesInterceptor`, `CrudOptions`, `MapCrudExtensions`,
`ModuleDbContext`; nel modulo `Tours/` (entità, stato, regole, endpoint, job) e la migrazione `AddTours`.
