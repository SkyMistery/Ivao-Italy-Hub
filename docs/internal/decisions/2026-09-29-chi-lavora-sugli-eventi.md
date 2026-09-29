# Chi lavora sugli eventi: cinque aree, chi collabora con `scope: ED`, chi collabora non cancella

**Data:** 29 settembre 2026 — fase E0 di M4
**Stato:** **decisa** (Carmine, 29 settembre 2026, sulla PR #180, [conferma di §17.1 e §17.2][ok]; §17.1 n.1, §17.2 n.2, come
raccomandato).
**Regola applicata:** `CLAUDE.md` §2 (un permesso è `<Area>.<Action>` nel catalogo del modulo, nessun handler nuovo) e §5, caso
**(b)**: i grant a una posizione con uno scope (`PositionGrantSeed.Scope`), i grant al team di un FIR (nota
`2026-09-27-i-capi-fir-sul-loro-fir`), `IHasStakeholder` e `DeniedToStakeholder` (nota
`2026-09-15-permessi-su-una-riga-e-chi-ha-interesse`). Design `09-design-m4.md` §0.3, §1.1, §6, §17.1 n.1, §17.2 n.2.

[ok]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/180#issuecomment-5880522987

## 1. Che cosa serviva decidere

- **Chi fa che cosa** (design §R.3, c1): **chiunque dell'ED** crea l'evento, carica gli slot e scrive gli stand; **le postazioni
  ATC** le decidono **l'AOD e lo staff dei FIR** (CH, ACH, CHA); **il FOD, chiunque del FOD**, scrive le **rotte**; **i no-show** li
  propone il sistema e li conferma uno staffista fra ED, AOD e staff del FIR; **i PIREP di supporto** li valida chi ne ha il
  compito, oggi l'MD; gli award li assegna, oggi, l'MD.
- **«Chi collabora non cancella»** (piano 0.77; nota `2026-09-13-ordine-dei-moduli` §3.3), che la nota temeva richiedesse «una
  sfumatura» nell'unico handler.
- **Chi risponde sui fatti dell'ED**: Carmine stesso (§17.1 n.1).

## 2. Le decisioni

1. **Cinque aree**, perché ogni capacità che si delega a un altro dipartimento è una riga sua (nota
   `2026-09-06-autorizzare-su-un-pezzo-di-un-altro-dipartimento`): **`Events`** (l'evento, gli scali, le regole di award, le
   domande di un evento in presenza), **`EventRoutes`** (le rotte), **`EventBookings`** (slot, prenotazioni, iscrizioni, attività),
   **`EventAtc`** (postazioni, disponibilità, turni), **`EventReports`** (i PIREP di supporto). Ogni area ha `View` ed `Edit`;
   `Events` ha anche `Delete` e `ManageSettings`. Il catalogo è quello del design §6.1.
2. **Negato all'interessato** (`DeniedToStakeholder`, vale anche per il superadmin): `EventAtc.Edit` (nessuno conferma il proprio
   no-show né approva la cessione del proprio turno) ed `EventReports.Edit` (nessuno valida il proprio PIREP).
3. **Chi collabora ha grant a una posizione con `scope: ED`** invece di entrare nel «a cura di» (§17.2 n.2): l'ED è in ogni evento
   (`ModuleBaseDepartment.Keep`), quindi AOD, staff dei FIR, FOD e MD lavorano su ogni evento **nella loro parte**. Il «a cura di»
   resta per chi cura **tutto** l'evento insieme all'ED (un evento con il SOD).
4. **I grant**, in `division.json → positionGrants` (design §6.2):
   - **EC ed EAC** tutto;
   - **EA1–9**: `Events.View` ed `Edit`, `EventRoutes.*`, `EventBookings.*`, `EventAtc.*`, `EventReports.View`;
   - **AOD, a tutti i livelli**: `Events.View`, `EventAtc.*`;
   - **staff dei FIR** (CH, ACH, CHA) con **`firTeam`**: `EventAtc.*`, sulle sole postazioni del loro FIR quando la divisione ha
     `firStaffScope: own`;
   - **FOD, a tutti i livelli**: `Events.View`, `EventRoutes.*`;
   - **MD, a tutti i livelli**: `Events.View`, `EventReports.*`;
   - HQ e web tutto per il nucleo, il superadmin tutto; `Awards.Assign` resta del nucleo. Chi sta fuori da queste posizioni riceve
     un grant a un VID, anche **su un evento solo** (`resource_scope`, `events:event:{id}`).
5. **Chi collabora non cancella, senza una sfumatura nell'handler** (§17.2 n.2): `Events.Delete` e `Events.ManageSettings` li hanno
   **solo EC ed EAC**, e nessuna riga di collaborazione li dà. È il modo di `Tours.Delete`, che sta solo a coordinator e assistant
   del FOD. Il limite, detto: chi un giorno desse `Events.Delete` a un altro dipartimento glielo darebbe davvero; è configurazione
   visibile.
6. **Le righe figlie dell'evento copiano maschera e scope dell'evento** a ogni scrittura (`CrudOptions.BeforeAuthorize`, come le leg
   di un tour); **le righe dei membri** (prenotazione, disponibilità, PIREP, cessione, iscrizione) sono `ISubmittedByMembers` e
   `IHasStakeholder`; **niente `IHasParticipants`**, che darebbe al membro `View` sull'area e i VID di tutti.

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| AOD, FIR, FOD e MD nel «a cura di» di ogni evento | lo staff dell'ED dovrebbe aggiungerli evento per evento, e il «a cura di» darebbe loro tutto l'evento, non la loro parte |
| Una sfumatura nell'unico handler per «chi collabora non cancella» | non serve: basta che nessuna riga di collaborazione dia `Events.Delete` |
| Un'area sola `Events` | il FOD scriverebbe anche le postazioni e l'AOD il testo dell'evento |
| AOD e MD solo coordinator e assistant | c1: «chiunque del FOD»; lo stesso per AOD e MD (§17.2 n.2) |

## 4. Che cosa si tocca, e dove

- **E2**: il catalogo delle cinque aree, `DeniedToStakeholder`, i `positionGrants` in `config/division.json` e in
  `config/division.example.json`; il controllo del design §6.3 (le schermate delle aree figlie leggono l'evento padre dai loro
  endpoint per chi non ha `Events.View`).
- **E3a–E17**: ogni entità nella sua area, come la tabella del design §6.1.
- **I test d'integrazione** di ogni fase: il FOD scrive le rotte e non gli slot; AOD e capo FIR l'ATC e non il testo; con `own` un
  capo FIR solo le postazioni del suo FIR; l'MD valida e non tocca il resto; chi collabora non elimina; nessuno decide la propria
  riga.

## Da portare nel piano

**Già nel piano 1.24**: §9.2 riga Events e §13 riga M4 (chi collabora ha grant con `scope: ED` e non cancella; `Events.Delete` solo
a EC ed EAC). **Nient'altro**: i grant di ogni modulo stanno nel suo design e in `division.json`, non nel piano (così per il TD in
M3). ⚠️ Per chi scrive E2: un grant del seme si ricorda uno per uno (`positionGrants.seeded`, piano 0.83), quindi uno aggiunto dopo
arriva anche a un database avviato, ma **uno tolto dopo non sparisce**: l'elenco del punto 4 va giusto la prima volta.
