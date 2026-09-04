# Una lista e il suo dettaglio sono tre route, non due

**Data:** 4 settembre 2026, poche ore dopo il tag di M0.
**Stato:** decisa da Carmine (opzione «layout + index + dettaglio»).

## Che cosa serve

Che cliccando «nuovo link» compaia il form.

## Il difetto

`staff.$dept.links.$id.tsx` era un route **figlio** di `staff.$dept.links.tsx`. In TanStack un figlio
si disegna dentro l'`<Outlet />` del padre; il componente della lista non ne rendeva nessuno — l'unico
`Outlet` del progetto sta nei tre layout. Quindi:

- l'indirizzo cambiava in `/staff/ed/links/new`;
- nessuna chiamata partiva (per `new` il loader non fetcha, correttamente);
- nessuna eccezione veniva lanciata;
- **la lista restava sullo schermo**, e il form non compariva mai.

Valeva per **tutte e tre** le coppie del back-office — `links`, `content`, `admin/permissions` —
quindi non si poteva creare o modificare un link, aprire l'editor di una pagina, né toccare un
grant. La metà «form» di F6 e F7 non è mai stata raggiunta in un browser.

Il sintomo che l'ha confermato senza bisogno di ipotesi è nell'URL che Carmine ha incollato:
`/staff/ed/links/new?page=1&pageSize=25&dir=asc`. Quei parametri sono la paginazione della lista, e
il dettaglio se li portava dietro **perché era suo figlio** e ne ereditava il `validateSearch`.

## Perché nessuno dei test l'ha visto

La stessa ragione del `TooltipProvider` di poche ore prima, e vale la pena che siano scritte una
accanto all'altra: **i test provano i pezzi, e niente prova la composizione**. Lì era la
composizione dei provider, qui è la composizione delle route. In entrambi i casi ogni singolo pezzo
era corretto — l'ho verificato durante la diagnosi, uno per uno: il router costruisce l'href giusto
(`/staff/ed/links/new`), `Button asChild` produce un vero `<a href>` e non un `<button>` che ingoia
il click, e la `parse` del padre non perde `id` al match. Tre ipotesi, tutte sbagliate, tutte
verificate con test usa-e-getta invece che a occhio: il guasto non era in nessun pezzo.

## La decisione

Ogni coppia lista/dettaglio diventa **tre route**:

1. `staff.<x>.tsx` — **layout**. Possiede ciò che entrambe le schermate condividono (il parse del
   dipartimento, la guardia su di esso o sul permesso) e rende `Outlet`.
2. `staff.<x>.index.tsx` — la **lista**: `validateSearch`, `loaderDeps`, `loader`, il componente.
3. `staff.<x>.$id.tsx` — il **dettaglio**, invariato.

Due effetti oltre alla correzione, entrambi desiderabili: la guardia è scritta **una volta** e vale
per tutte e due le schermate, e i search params della lista **smettono di seguire il form**.

### L'alternativa scartata

**Un `<Outlet />` dentro ogni componente di lista.** Una riga per file invece di tre file. Scartata
perché il form comparirebbe **sotto la tabella**, con la lista ancora caricata e visibile: è un
layout master-detail che nessuno ha progettato, sarebbe strano per «nuovo link», e non toglierebbe
i search params dall'URL del dettaglio. Soprattutto, lascerebbe la struttura in cui il difetto è
possibile: basta che il prossimo che scrive una lista dimentichi la riga.

## La rete

`web/e2e/back-office.spec.ts`, quattro smoke con una **sessione staff finta** (`stubTheApiAsStaff`):
la lista si apre e mostra la sua riga, «nuovo» arriva al form *e la lista sparisce*, «modifica»
arriva al form di quella riga, e un dipartimento che il membro non raggiunge è un rifiuto e non una
tabella vuota.

L'identità finta è un coordinatore con `hasAllDepartments: false` e un solo dipartimento, **non** un
superadmin: è l'unica che esercita davvero la guardia sul dipartimento.

Le asserzioni sono deliberatamente su **due metà insieme** — l'indirizzo è cambiato *e* la cosa che
prometteva è sullo schermo. Una metà sola è esattamente ciò che ha lasciato passare il difetto.

Verificata togliendo l'`Outlet`: **tre dei quattro smoke falliscono**. Un test di regressione che
passa in entrambi i casi non è un test, ed è la seconda volta in un giorno che questo controllo
serve a qualcosa.

## Che cosa se ne impara

Il design §7.3 documentava la ricetta 2 **nella forma sbagliata**, in un file solo, ed è stato
copiato tre volte fedelmente. Una ricetta che si copia è un moltiplicatore: quando è giusta fa
risparmiare, quando è sbagliata replica il difetto ovunque senza che nessuno lo rimetta in
discussione — perché copiarla è precisamente ciò che il progetto chiede di fare. §7.3 è ora
corretta e porta l'avvertimento accanto al codice.
