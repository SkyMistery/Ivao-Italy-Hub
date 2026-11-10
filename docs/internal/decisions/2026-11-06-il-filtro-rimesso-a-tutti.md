# Un filtro rimesso a «tutti» disegna «tutti»

**Data:** 6 novembre 2026, branch `fix/list-filter-back-to-all`, dopo la PR #466.
**Stato:** registra una differenza fra due correzioni dello stesso difetto. Nessuna scelta da fare per Carmine: la differenza non si vede a schermo. Lo
scrive la sessione di lavoro, incaricata dal master.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: nessun meccanismo nuovo. È la «causa 2» di `2026-11-06-il-campo-rimesso-a-niente.md`, che lì era stata
lasciata fuori perché il filtro non è il form generato.

## Il difetto, misurato

Scelto un valore e tornati a «tutti», l'elenco è di nuovo intero, ma il `Select` del filtro disegna ancora la scelta vecchia. Radix, senza `value`,
passa a non controllato e ridisegna l'ultima che teneva. Senza il valore, il test nuovo (`ListFilter.test.tsx`) è rosso su 3 di 7:

- **Il ritorno a «tutti»**: tiene niente e disegna `Alpha`.
- **Il filtro azzerato da fuori** (l'indirizzo che cambia, per esempio): stesso sintomo.
- **La stessa scelta non si può rifare.** Per Radix il valore è ancora `alpha`, quindi riscegliere `Alpha` non è un cambiamento e `onValueChange` non parte:
  la persona preme e l'elenco resta intero. Solo un'altra scelta in mezzo sbloccava il filtro.

## Com'è andata: la correzione c'era già

Mentre questa PR aspettava la CI, la #462 è entrata in `main` con la stessa correzione, nata da un altro lavoro (commit `8d7e7e6b`): `value={value ?? NO_CHOICE}`,
senza un test. Questa PR aveva scritto `value ?? ''`, come la nota sul form. **Resta la riga di `main`**, e qui restano solo ciò che le mancava: i test e questa
nota. Non cambiando il prodotto, questa PR non alza la versione.

## Perché `NO_CHOICE` qui e `''` nel form

La nota sul form scarta `NO_CHOICE` perché mostrerebbe la parola «nessuno» dove un form nuovo non ne mostra. Per il filtro quel motivo non vale: il segnaposto e la
voce «tutti» sono la stessa parola, `none`. Le differenze, lette nel codice:

- **A schermo chiuso, nessuna**: Radix toglie `data-placeholder`, ma Atmosphere non gli dà uno stile (cercato in `dist/`) e nemmeno il hub. Provato su `/calendar`.
- **A elenco aperto**: la voce «tutti» è quella segnata, e con `''` non lo sarebbe nessuna. È il filtro che dice che cosa tiene.

Scrivere `''` qui avrebbe disfatto una correzione unita per una differenza che nessuno ha chiesto.

## `LocaleSwitcher`: nessuna modifica

Passa un `value` facoltativo, ma non è lo stesso difetto. `current` è `undefined` solo se la divisione non ha alcuna lingua (`locales` vuoto), e allora
non c'è niente da scegliere né da disegnare. Con almeno una lingua `current` ricade su `locales[0]`, quindi è sempre una stringa, e non esiste la voce per
tornare a «niente». Letto, non misurato a schermo: non c'è un caso da provocare.

## Da portare nel piano

- **§16** (meccanismi generici), accanto alla regola portata dalla nota `2026-11-06-il-campo-rimesso-a-niente`: un `Select` riceve sempre un `value`. Nel form
  generato è `''` per niente; in un filtro, che ha una voce per «tutti», è il valore di quella voce (`NO_CHOICE`).
