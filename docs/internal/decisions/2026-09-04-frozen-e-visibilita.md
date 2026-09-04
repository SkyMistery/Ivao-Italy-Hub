# La cattura `frozen` vede quello che vede chi pubblica

**Data:** 4 settembre 2026 — F7
**Stato:** implementato **come il design lo descrive**; la conseguenza va decisa da Carmine

## Il fatto

Design §5.5: alla pubblicazione, «per ogni blocco `kind = data` con `renderMode = frozen` chiama
`IDataBlockProvider(type).ResolveAsync(props, currentUser)` e scrive il risultato in `frozen`».

`LinkListProvider` legge `cms_links` **senza** `IgnoreQueryFilters`, cioè attraverso il global query
filter di §3.5, che è la cosa giusta: è un lettore come gli altri. Il filtro però risponde a
`ICurrentUser`, e in fase di pubblicazione `ICurrentUser` è **lo staff che sta pubblicando**.

Quindi: un coordinatore che pubblica una pagina `Public` con un `linkList` `frozen` cattura anche i
link `Staff` e `Members` che lui vede — e quella cattura finisce nella versione, che è ciò che legge
un anonimo. Il filtro di visibilità protegge la riga `cms_contents`, non il contenuto che qualcuno
ha copiato dentro la versione.

Non è un buco nel meccanismo: è il meccanismo applicato al momento sbagliato. Il percorso `live` è
corretto per costruzione (`GET /api/blocks/data/{type}` risolve con l'`ICurrentUser` del lettore).

## Perché non l'ho corretto da solo

La correzione ovvia — risolvere un blocco `frozen` con la visibilità del **contenuto** invece che
con quella di chi pubblica — vuole una seconda strada per la visibilità accanto al query filter, o
un `ICurrentUser` sintetico costruito dalla riga. È un meccanismo nuovo: regola (c) di CLAUDE.md §5.

## Le opzioni

1. **Lasciarlo così e dirlo nell'editor.** Un blocco `frozen` mostra un avviso: «cattura quello che
   vedi tu adesso». Costo: una chiave i18n. Rischio: dipende da chi legge l'avviso.
2. **`DataBlockContext`**: la pubblicazione passa al provider anche `Visibility` e
   `OwnerDepartment` del contenuto, e un provider che legge righe `IVisible` scarta quelle più
   riservate della pagina. È un contratto in più su `IDataBlockProvider`, non una seconda copia del
   filtro. Costo: una riga nell'interfaccia e una nel provider; va nel design §5.5.
3. **Congelare solo il pubblico.** Un blocco `frozen` cattura sempre come farebbe un anonimo. Più
   semplice di (2) e più restrittivo di quanto probabilmente serve (una pagina `Staff` con un
   elenco di link `Staff` catturerebbe zero righe).

**Raccomandazione: (2).** È l'unica che dice la cosa vera — una cattura non può essere più visibile
della pagina che la contiene — e non riapre il query filter.

## Nel frattempo

F7 è chiusa con il comportamento del design (opzione 0). Il test
`ContentPublishFreezesDataBlocks` cattura link `Public`, quindi non copre il caso; è scritto qui
apposta perché non si perda.
