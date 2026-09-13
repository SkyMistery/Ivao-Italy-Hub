# L'ordine dei moduli, e chi li gestisce

**Data:** 13 settembre 2026, notte — portata da Carmine dopo il confronto con lo staff di IVAO,
subito dopo la chiusura delle dashboard (D3, PR #77)
**Stato:** **decisa** (Carmine, 13 settembre 2026) per §3.1–§3.4; **§3.5 è una proposta**, da
decidere prima che parta il secondo modulo
**Regola applicata:** `CLAUDE.md` §5: nessun meccanismo nuovo. Cambia l'ordine della roadmap
(piano §13) e si precisa chi tiene i permessi dei moduli dentro la nota
`2026-09-13-moduli-non-subordinati-ai-dipartimenti`, che resta valida.

## 1. Che cosa serve

- **I moduli si fanno in quest'ordine: Tours, Training, Eventi.**
- Restano **esterni ai dipartimenti** (già deciso): il FOD ha tutti i diritti sui tour, il TD sui
  training, l'ED sugli eventi; HQ e superadmin su tutto.
- **Per ora non servono altri moduli**, e i dipartimenti vanno bene come sono.
- Carmine chiede se due Claude Code, su due PC di due staffisti, possono lavorare insieme: uno a Tours,
  l'altro a Training.

## 2. Che cosa esiste già

- Il piano (§13, deciso il 1° set 2026) diceva **Events → Tour → Training**: «il tour ha già design e
  validatore, Training è il modulo più complesso».
- La parte A di M2 ha costruito la base comune ai tre moduli: grant a una posizione (H1), righe in cura
  a più dipartimenti con il **dipartimento di base** in `division.json → modules.<key>.baseDepartment`
  (H2: `flightops → FOD`, `training → TD`, `events → ED`), sezioni dei moduli nella barra dello staff
  (H3). Nessun modulo di dipartimento è ancora scritto: `05-design-m2.md` non esiste.
- Chi arriva a ogni dipartimento — Director, Assistant Director, web team, superadmin — tiene già ogni
  permesso, anche quelli dei moduli.

## 3. Le decisioni

### 3.1 L'ordine: Tours, Training, Eventi (Carmine)

- **M2 — Tour** (modulo `flightops`), **M3 — Training**, **M4 — Eventi**. Il documento di design del
  primo resta `05-design-m2.md`, e diventa quello dei tour; la parte C di
  `06-piano-implementazione-m2.md` è il modulo Tours.
- **Il pacchetto self-contained e il deploy su staging Plesk restano in M2**: non dipendono dal modulo
  e aspettano sempre le risposte A9 (§15.2c). `ivao-booking` si spegne con gli eventi, quindi con M4.
- **Nessun altro modulo in roadmap per ora**: `specialops` resta un segnaposto senza milestone e
  `atc` resta tolto. I dipartimenti restano come sono.

### 3.2 Chi gestisce un modulo (Carmine)

- **Coordinator e assistant del dipartimento di base hanno tutte le funzioni del modulo**: FOD sui
  tour, TD sui training, ED sugli eventi. HQ e superadmin su tutto, come già oggi.
- **Che cosa possono fare gli advisor si decide modulo per modulo**, nel suo documento di design.
- **Come, nel codice**: con il meccanismo già deciso, i **grant a una posizione** seminati da
  `division.json → positionGrants` (nota `moduli-non-subordinati` §3.2: «i permessi del nucleo restano
  in `RolePermissionMatrix`; i grant per posizione servono ai permessi dei moduli»). Il design di ogni
  modulo scrive l'elenco dei suoi permessi e le righe di `positionGrants` per coordinator e assistant
  del dipartimento di base, e per gli advisor quello che decide.
- ⚠️ In chat Claude aveva proposto una regola nel codice («il dipartimento di base tiene i permessi del
  modulo»). **Scartata**: riapre una scelta già fatta, e ogni divisione che forka ha comunque
  un'organizzazione sua da scrivere nella configurazione.

### 3.3 La collaborazione resta, la cancellazione no (Carmine)

- **«A cura di» multiplo resta** (nota `moduli-non-subordinati` §3.3): un evento dell'ED può essere in
  collaborazione con il SOD, e il SOD lo modifica.
- **Il dipartimento che collabora non cancella**: una riga la cancellano solo coordinator o assistant
  del **dipartimento di base**.
- **Si precisa con il design degli eventi** (M4). ⚠️ Da guardare allora: oggi l'unico handler concede
  un permesso tenuto su **uno qualsiasi** dei dipartimenti della riga, quindi «cancellare chiede il
  permesso sul dipartimento di base» è una sfumatura da dare a quel meccanismo, non un secondo handler.
  Vale anche per tour e training se ci sarà collaborazione.

### 3.4 Che cosa non cambia

- I moduli esterni ai dipartimenti, `/staff/tours`, `/staff/training`, `/staff/events`, «a cura di»
  con il dipartimento di base, le tessere delle dashboard come blocchi Data.
- Ogni modulo riceve il suo documento di design prima del codice.
- La chiave del modulo dei tour resta **`flightops`** (piano §9.2, `division.json`, schema `fo_`);
  «Tours» è il nome della sezione.

### 3.5 Due agenti su due PC — **proposta, non ancora decisa**

**Si può.** I due Claude Code non si parlano: si coordinano attraverso GitHub (branch, PR, CI) e i
documenti del repository. La struttura aiuta: ogni modulo ha la sua cartella, il suo `DbContext` con
la sua storia delle migrazioni e i suoi namespace di lingua.

**Cosa va fatto prima:**

1. **Le regole di lavoro devono stare nel repository.** Oggi stanno in `CLAUDE.md`, che è privato e
   fuori dal repository, e nelle memorie locali di Claude: l'altro agente non le vedrebbe. Vanno in un
   file versionato (per esempio `docs/internal/REGOLE-DI-LAVORO.md` o un `AGENTS.md`), e le lezioni
   delle memorie che contano (i VID dei test, i file generati, il DB dei test che si accumula) nei
   documenti.
2. **Un modulo, un documento di design e un piano di implementazione suoi.** Il piano generale
   (versione e changelog) lo aggiorna solo chi decide, dopo il merge: due branch che alzano la stessa
   versione vanno sempre in conflitto.
3. **Un cambio al nucleo è una PR a sé, piccola, mergiata prima**, e l'altro modulo ci si riallinea.
   Due agenti che estendono lo stesso meccanismo in parallelo ne scriverebbero due versioni.
4. **File generati e condivisi** (`schema.d.ts`, `Modules.cs`, `modules/index.ts`, i conteggi nei
   test): nei conflitti si rigenerano, non si uniscono a mano.
5. **Il database dei test è condiviso**: ogni modulo ha un intervallo di VID e un prefisso di slug suoi.
6. **Accessi**: l'altro staffista è collaboratore GitHub, `main` protetto (PR e CI obbligatorie),
   mergia Carmine; niente segreti di produzione; credenziali OAuth IVAO di test sue.

**L'ordine che Claude consiglia:** Tours parte da solo, perché il primo modulo scopre che cosa manca al
nucleo e fissa le convenzioni (struttura del modulo, permessi da `positionGrants`, blocchi Data,
test). Nel frattempo l'altro staffista può scrivere il **documento** di design di Training, che non
tocca codice. Training entra nel codice quando le prime fasi di Tours (scheletro e cambi al nucleo)
sono mergiate.

## 4. Che cosa si tocca

- **Piano**: §13 (l'ordine e il testo di M2–M4), §9.2 (una riga su `specialops` e sull'ordine), il
  changelog 0.77.
- **`06-piano-implementazione-m2.md`**: l'intestazione e la parte C.
- **`HANDOFF.md`** e **`CLAUDE.md`** §8.
- **Codice**: niente adesso. Le righe di `positionGrants` arrivano con il design di ciascun modulo.
