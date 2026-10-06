# Il pubblico degli eventi e le rotte: cinque letture del design (E4)

**Data:** 6 ottobre 2026 — fase E4 di M4 (il pubblico e le rotte), PR #223
**Stato:** **decisa** (Carmine, 6 ottobre 2026, in chat al master e pubblicata sulla #223 su sua istruzione: [la risposta][ok223], autore
`SkyMistery`): **sì** alle cinque letture come sono scritte qui sotto, «mai da uno scalo a sé stesso» compreso, e **sì alla seconda lettura
scritta a mano** (§4). La domanda era andata a lui con un commento sulla PR, come le tre regole di E3b, che il revisore gli aveva portato
sulla #221 e che lui aveva voluto in una nota ([le sue risposte][a221]).
**Regola applicata:** `CLAUDE.md` §5, casi **(a)** e **(b)**: nessun meccanismo nuovo — il filtro globale, l'unico handler, `MapCrud`, il
blocco Data, `Localized<T>` —; sono letture del design M4 §1.4, §2.3, §2.4, §7.1, §7.3.

[a221]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/221#issuecomment-6012333670
[r223]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/223#issuecomment-6014660539
[ok223]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/223#issuecomment-6017107039

## 1. Le letture

1. **La pagina di un evento allo staff degli eventi, in ogni stato.** Il design §2.4 dice che dopo la fine «la pagina risponde 404 a chi
   non è staff»; non dice delle bozze, né di un evento pubblicato e non ancora visibile. E4: chi ha **`Events.View` sulla riga** —
   chiesto all'unico handler: l'ED, e chi collabora (AOD, FOD, MD) per il suo grant sull'ED — legge `/events/{slug}` in **ogni** stato,
   bozza, programmato, concluso, con una riga che dice che nessun altro la vede **e perché** — bozza, non ancora visibile, concluso:
   il motivo lo dà il server (`EventState.Unseen`), perché lo stato da solo non distingue un annullato non ancora visibile da uno
   concluso (dal banco, 6 ottobre 2026) —; tutti gli altri solo mentre l'evento si vede
   (`EventState.IsSeen`, per chi è l'evento), e 404 altrimenti. Perché: una condizione sola invece di due; lo staff vede la pagina prima di
   pubblicare, e «Pubblica» non ha un contrario (E3b); chi collabora legge lì la descrizione, che nel back office è una scheda di chi scrive
   l'evento (E3a, scelta 7). Alternativa: allo staff solo dopo la fine, alla lettera, e una bozza solo nel back office, come i tour.
2. **Un evento annullato** resta nelle schede di `/events` e nel blocco fino alla sua fine, con lo stato «Annullato» e, sulla pagina, la
   nota (§2.3); **non** è nel calendario sotto le schede, come non è nel calendario della divisione (E3b, §8.1).
3. **Il filtro per scalo** di `/events` tiene gli eventi che nominano quello scalo; un evento di tutta la divisione (l'online day) non ne
   nomina nessuno, e compare solo senza filtro. Le scelte dei due filtri sono quelle che le schede hanno, come in `/calendar`. Alternativa:
   un evento di tutta la divisione sotto ogni scalo — ma fra le scelte ci può essere uno scalo fuori dalla divisione (un RFE con un capo
   all'estero), e «della divisione» lo dice solo `IvaoAirspace`, che il modulo non nomina.
4. **Le rotte** (§1.4): le note (`remarks`) sono **tradotte** (`remarks_i18n`), e scritte in una lingua si scrivono in tutte quelle della
   divisione, perché la pagina le mostra a tutti — come la nota di un annullamento (E3a) e le parole sotto il link dell'esame teorico (A13a);
   la **rotta** è obbligatoria e non tradotta (sono parole della rete), al più 1024 caratteri, le note 500 per lingua; **più rotte fra gli
   stessi due scali** sono ammesse (niente indice univoco: le note dicono quale è quale); nessun vincolo che uno dei due capi sia uno scalo
   dell'evento (un evento di tutta la divisione non ne ha); **si ordinano come il FOD le scrive** — sulla pagina e nella scheda del back
   office, dove si riordinano per scalo a mano —, perché l'ordine per partenza metteva il ritorno LIMC→LIRF prima dell'andata
   LIRF→LIMC (dal banco, 6 ottobre 2026). **Una rotta va da uno scalo a un altro**: da
   uno scalo a sé stesso, comunque scritto, il validatore la rifiuta sull'arrivo (`events:errors.routeToItself`), come ha chiesto il
   revisore ([punto 4][r223]).
5. **Eliminare un evento** porta via le sue rotte nello stesso salvataggio, ognuna con la sua riga di audit, come gli scali (E3a). Il
   guardiano chiede per ognuna `EventRoutes.Edit`, che chi elimina un evento in IT ha (EC ed EAC tengono tutte le aree, §6.2). ⚠️ Una
   divisione che desse `Events.Delete` senza `EventRoutes.Edit` si vedrebbe rifiutare (403) l'eliminazione di un evento con rotte: toglie
   prima le rotte.

## 2. Che cosa si è toccato

Solo il modulo: `EventRoute.cs`, `EventState.cs` (`Unseen`), `Staff/EventRouteEndpoints.cs`, `Staff/EventSaving.cs`,
`Public/PublicEvents.cs`, `Public/EventListProvider.cs` e le schermate di `web/src/modules/events/`.

## 3. La domanda a Carmine

> Cinque comportamenti delle pagine pubbliche e delle rotte che il design non dice, scritti in E4 come raccomanda questa nota: (1) lo staff
> degli eventi legge la pagina di un evento in ogni stato, bozze comprese, con una riga che dice che nessun altro la vede; (2) un annullato
> resta nelle schede fino alla fine, ma non nel calendario; (3) il filtro per scalo non mostra gli eventi di tutta la divisione; (4) le
> note delle rotte tradotte, e più rotte fra gli stessi due scali; (5) eliminare un evento elimina le sue rotte con l'audit. Confermi?

## 4. La seconda lettura scritta a mano

**`GET /api/events/public/{slug}`** (`Public/PublicEvents.cs`, `PublicEventEndpoints`): la pagina di un evento, anonima, composta in una
lettura sola, come la lettura pubblica dei tour. Il design §7.2 dice che gli endpoint scritti a mano sono verbi; E4 l'aveva scritta fra
gli scostamenti di «Com'è andata» (scelta 6), e il revisore l'ha portata a Carmine ([i rilievi sulla #223][r223], punto 2). Carmine l'ha
**accettata** come la lettura dei preset (nota `2026-10-01-la-lettura-dei-preset-dei-tipi`): uno **scostamento dichiarato** dal design
§7.2, contato per il piano §16.6 ([la risposta][ok223], punto 3). Gli endpoint a mano di E4 sono quindi uno, questa lettura; le rotte sono
`MapCrud`, e la lista di `/events` è il blocco `events.eventList`, letto come ogni blocco.

## Da portare nel piano

Con la risposta: il design M4 §2.4 e §7.1 (la pagina allo staff in ogni stato, con il motivo; l'annullato nelle schede e non nel
calendario; il filtro per scalo), §1.4 (le colonne delle rotte, con le note tradotte, mai da uno scalo a sé stesso, nell'ordine in cui
sono scritte) e §2.3 (eliminare porta via le rotte); il piano §9.2, riga Events, se Carmine lo ritiene. E, come per la lettura dei
preset: il piano **§16.6** (fra gli endpoint a mano di M4, la lettura della pagina di un evento, `GET /api/events/public/{slug}`,
anonima) e il design **§7.2** (accanto a «gli endpoint a mano sono verbi», questa lettura con il link a questa nota).
