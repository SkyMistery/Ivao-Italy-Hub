# `MarkdownContent`: quale renderer

**Data:** 3 settembre 2026 — F6
**Stato:** **decisa da Carmine** (3 set 2026)

## Il problema

Il design §7.1 mette `MarkdownContent` nell'elenco chiuso dei componenti custom di M0, ma §0.3 non
pinna nessuna libreria che sappia rendere Markdown. F6 deve montare ogni componente dell'elenco in
`/staff/admin/ui-kit`, quindi il componente serve; come renda il Markdown, il design non lo dice.

## Le opzioni

1. **`react-markdown`** come dipendenza diretta, pinnata.
2. **Rimandare a F7**, quando arrivano i blocchi editoriali che ne hanno davvero bisogno: la fase si
   chiude senza quel pezzo (piano §A.3 lo ammette esplicitamente) e la ui-kit resta corta di un
   componente.
3. **Scriverne uno minuscolo a mano** (paragrafi, grassetto, corsivo, link, liste, codice).

## La decisione

**Opzione 1**, scelta da Carmine. `react-markdown@10.1.0`, dipendenza diretta di `web/`.

Perché non la 3: un parser scritto a mano è codice che possediamo per sempre, e la prima cosa che
gli si chiede in F7 — tabelle, o HTML da rifiutare per bene — lo manda a gambe all'aria. Sarebbe
stato buttato via appena i contenuti veri fossero arrivati.

Perché non la 2: la ui-kit di F6 è anche il test «ogni componente dell'elenco compare», e chiuderla
con un buco significa aprire F7 con un test già indebolito.

## Cosa comporta

- `react-markdown` costruisce un albero React e non tocca mai `innerHTML`; l'HTML grezzo nel sorgente
  **non** è abilitato (`rehype-raw` non c'è). Un `<script>` scritto da un editor resta quattro parole
  visibili. È la ragione principale per cui è questa la libreria e non una che produce stringhe HTML.
- Pesa: finisce in un chunk suo (`manualChunks` in `vite.config.ts`, ~118 kB), caricato solo dalle
  pagine che mostrano prosa. Non entra nel bundle di chi apre `/staff/ed/links`.
- I link che un editor scrive sono link verso l'esterno: `target="_blank"` con
  `rel="noreferrer noopener"`, deciso nel componente e non nel contenuto.
- Le classi degli elementi sono passate componente per componente e non da un foglio di stile
  globale: lo stesso `MarkdownContent` compare dentro un blocco, dentro una card e nella ui-kit, e
  una regola globale le colpirebbe tutte e tre che le stia bene o no.

## Da riguardare

Se in M1 servono le tabelle Markdown, si aggiunge `remark-gfm` (stesso autore, stessa famiglia) e
basta. Se invece servisse l'HTML grezzo dentro i contenuti, **non** si aggiunge `rehype-raw` senza
una decisione nuova: quella è una scelta di sicurezza, non di formattazione.
