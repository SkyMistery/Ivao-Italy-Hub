# Il menu del sito sul telefono

**Data:** 24 ottobre 2026
**Stato:** **Proposta** — scritta da `gabbosonc31` per la #381; la domanda è a Carmine, sulla issue. Nessun codice prima della risposta.
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: la correzione chiede una forma nuova della barra del sito, e l'unico pezzo di
Atmosphere fatto per questo (`Sheet`) non si può usare così com'è. Il caso è al confine con la lista chiusa dei componenti
(`docs/UI-GUIDELINES.md` §3), quindi la scelta è di Carmine.

## 1. Che cosa serve

Misurato sulla prova (0.10.5) e uguale su `main` (0.12.1), visitatore senza accesso, sito in italiano:

| Larghezza | Che cosa succede |
|---|---|
| 375 px (telefono) | si vedono «Home» e mezzo «Come si comincia»; la pagina è larga **1347 px** e scorre di lato |
| 1024 px (tablet orizzontale) | uguale: pagina larga 1347 px |
| 1280 px (portatile) | uguale: pagina larga 1347 px |
| 1366 px e oltre | ci sta |

Il pulsante **«Accedi con IVAO»** sta a x = 1347: sotto quella larghezza **non si vede** se non scorrendo la pagina di lato, e niente
dice che bisogna farlo. Il contenuto delle pagine invece sta in 375 px: esce solo la barra.

La barra è **una riga sola** per scelta (Carmine, 10 settembre 2026, nota `2026-09-10-la-barra-la-sidebar-e-il-footer` §1 e il
commento in `web/src/app/layouts/Chrome.tsx`): logo e nome, il `NavigationMenu` al centro, gli strumenti e l'account a destra. Non ha
una forma stretta: l'unico ramo `md:hidden` riguarda il logo (`Chrome.tsx:115`). E la larghezza che serve **non è fissa**: dipende
dalle voci del menu, che scrive lo staff, e dalla lingua. Un fork con voci più lunghe o più numerose ci arriva prima.

## 2. Perché non basta quello che c'è

- **`Navbar` di Atmosphere** non ha una forma per il telefono (e la barra usa già `NavbarContainer` al suo posto, per centrare il menu).
- **`Sheet` di Atmosphere** è il pannello a scomparsa fatto per questo, ma `SheetContent` disegna il suo pulsante di chiusura con
  **«Close» scritto nella libreria** (`sr-only`, e una seconda volta nel piede con `footerCloseButton`): lo stesso difetto che ha
  fatto sostituire il `Sidebar` di Atmosphere con `StaffSidebar` (`docs/UI-GUIDELINES.md` §3). Usato così, la regola 1 (nessuna
  stringa visibile che non sia una chiave) non si tiene: il lettore di schermo dice «Close» a un utente italiano.
- **`DropdownMenu` di Atmosphere** non scrive niente di suo: le voci, l'etichetta del pulsante e i sottomenu sono nostri. È già
  usato nell'hub (per esempio «Aggiungi dopo» nella tabella delle tratte, `LegGrid.tsx`).

## 3. Che cosa si tocca

`web/src/app/layouts/Chrome.tsx` (la barra del sito; è nucleo), le sue chiavi in `locales/*/common.json` (l'etichetta del pulsante
del menu), `Chrome.test.tsx`, e una prova a 375 px nella suite e2e (come `web/e2e/back-office.spec.ts` misura già la geometria del back office). Niente
lato server, niente migrazioni.

## 4. Le strade

**A. Sotto la larghezza che serve, un pulsante «Menu» con un `DropdownMenu` di Atmosphere.** *(raccomandata)*
Il logo e il nome restano a sinistra; al posto del `NavigationMenu` e degli strumenti compare un pulsante con l'icona `Menu` di
`lucide-react` e un'etichetta tradotta. Il menu che apre ha le stesse voci di `navigation.public` (una voce con figli diventa un
sottomenu, come già fa il `NavigationMenu`), poi lingua, tema, «Staff» se tocca, e l'accesso o l'uscita. **Nessun componente
nuovo**: è `DropdownMenu` di Atmosphere composto nella barra, come la barra compone già `NavbarContainer` e `IVAOLogo`.
- **Quando passare al pulsante**: non a una larghezza fissa (oggi servono 1347 px, domani dipende dallo staff e dal fork), ma
  **quando la riga non ci sta**: un `ResizeObserver` sulla barra confronta la larghezza che la riga chiede con quella che ha. Una
  soglia fissa (`xl`, 1280 px) è più semplice ma oggi sbaglierebbe già, perché a 1280 px la riga non ci sta.
- Costo: una trentina di righe in `Chrome.tsx`, una chiave, un test unitario, una prova e2e a 375 px.

**B. `Sheet` di Atmosphere, il pannello a scomparsa.** È il disegno più comune sui telefoni. Ma richiede di aggirare `SheetContent`
(il suo «Close» fisso): o un pannello nostro sopra `SheetRoot`/`SheetPortal` e il `Dialog` di Radix, cioè un **componente nuovo nella
lista chiusa** (`SiteMenuSheet`), o una modifica ad Atmosphere che non è nostra. Scartata come prima scelta; resta la strada se Carmine
vuole proprio un pannello laterale.

**C. La riga che va a capo** (`flex-wrap`): nessun componente, poche classi. Ma su un telefono sette voci diventano tre o quattro righe
sopra ogni pagina, e va contro la «riga sola» del 10 settembre. Scartata.

**D. Togliere voci dalla barra sotto una soglia** (solo logo e accesso, il menu nel piè di pagina): il visitatore del telefono perde
il menu proprio dove serve. Scartata.

## 5. La domanda per Carmine

**Sul telefono (e dove la riga non ci sta), la barra del sito diventa logo + pulsante «Menu» con un `DropdownMenu` di Atmosphere,
che passa a quella forma quando la riga non ci sta più (misurata), non a una larghezza fissa?**
Raccomandazione: **sì, la A**, con il passaggio misurato. Se preferisci un pannello laterale, la B, sapendo che è un componente nuovo
nella lista chiusa.

## Da portare nel piano

- **§8.3** (i componenti): nessun componente nuovo con la A; con la B, `SiteMenuSheet` nella lista chiusa.
- **`docs/UI-GUIDELINES.md`**: la barra del sito ha due forme, la riga e il pulsante «Menu», e quando passa dall'una all'altra.
- **Design M0 §7.1** (il layout pubblico): la forma stretta della barra.
