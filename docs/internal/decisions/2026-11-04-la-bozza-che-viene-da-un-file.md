# La bozza che viene da un file: una colonna sulla riga, per la riga della versione

- **Data**: 4 novembre 2026
- **Stato**: **decisa — Carmine in chat al master, 4 novembre 2026 («Campo sul server»)**. Non c'è un link perché la risposta è in chat: la nota è di una sessione di Carmine, non del collaboratore
- **Chi**: una sessione di lavoro di Carmine, sul rilievo del revisore alla #453
- **Classificazione** (`CLAUDE.md` §5): **(b)**. Serve una colonna, ma nessun meccanismo nuovo: la scrive il salvataggio generato di `cms_contents`, la toglie il servizio di pubblicazione, e la tiene fuori dall'audit `[NotAudited]`, che esiste già. Tocca il nucleo, quindi è una PR a parte con la sua nota, prima della #453 che la usa
- **Completa** la risposta 6 del §8 di `2026-10-30-una-pagina-scritta-con-claude.md`, che non si riscrive

## 1. Che cosa serve

La nota del 30 ottobre (§3.3, risposta 6) vuole che la riga «che cosa è cambiato» della versione parta da «Importata da Claude», e che lo si dica lì e non nell'audit. Non dice dove si ricorda, tra l'import e la pubblicazione, che la bozza viene da un file.

La prima stesura della 3.0b lo teneva nel `localStorage` di chi importa. Regge un ricaricamento, ma sulla strada normale di una pagina chi importa non è chi approva: l'approvatore, su un altro browser, non vede niente, e la storia delle versioni non dice mai che la pagina viene da Claude. La domanda è andata a Carmine, che ha scelto il campo sul server.

## 2. Che cosa si fa

- **La colonna**: `cms_contents.draft_from_file`, `ContentEntry.DraftFromFile`, booleana, falsa per ogni riga che c'è già. Migrazione solo additiva nel contesto del nucleo. Il nome non nomina Claude: al server non importa chi ha scritto il file.
- **Chi la scrive**: un salvataggio che la nomina, con `ContentWriteDto.DraftFromFile`: `true` l'import, il valore di prima Annulla, e di nuovo `true` Ripeti.
- ⚠️ **Un salvataggio che non la nomina non la tocca.** Il campo è `bool?` e l'assenza non vuol dire `false`: l'autosalvataggio e una pressione su «Salva bozza» non ne sanno niente, e la cancellerebbero alla prima pausa dopo l'import. È il caso facile da sbagliare, e ha il suo test (`ContentDraftFromFileTests`).
- **Chi la toglie**: la pubblicazione, in `ContentPublishService`, nella stessa transazione della versione. Anche l'approvazione passa da lì. Dopo l'uscita, la bozza nuova non viene da un file finché qualcuno non ne importa uno.
- **Chi la legge**: il dettaglio della riga (`ContentDetailDto.DraftFromFile`), quindi l'editor e il pannello dell'approvatore, che lo stesso dettaglio già lo caricano. La parola resta quella di `content.import.versionLine`.
- **L'audit non cambia** (risposta 6): `[NotAudited]` la tiene fuori dalle copie (piano 1.48), e un salvataggio che cambia solo lei non scrive una riga di audit e non muove `updated_at`.
- ⚠️ **Una riga in più nell'interceptor.** L'autosalvataggio scrive nell'audit i nomi delle colonne cambiate (`ChangedColumns`), e quel nome passava anche per una colonna `[NotAudited]`: l'Annulla salvato da solo avrebbe scritto `DraftFromFile` nell'audit. `ChangedColumns` ora le salta tutte, come già fanno le copie. Vale per ogni colonna segnata così, e nessuna di quelle di oggi cambia in un autosalvataggio.
- **Il permesso**: nessuno di nuovo. Chi scrive la bozza con `Content.Edit` dice anche da dove viene; il valore non decide niente, serve solo a proporre le prime parole di una riga che chi pubblica può riscrivere.

## 3. Alternative

| | Che cosa | Pro | Contro |
|---|---|---|---|
| **A** | **Una colonna sulla riga** (decisa) | Arriva a chi approva, regge un ricaricamento e un altro browser, si toglie alla pubblicazione nella stessa transazione | Una migrazione e una colonna per una riga di testo |
| B | Il `localStorage` di chi importa (la prima stesura della #453) | Niente server | Non arriva all'approvatore, cioè sulla strada normale di una pagina |
| C | Leggere l'audit | Niente colonna | La risposta 6 tiene l'import fuori dall'audit, che oggi non ha un campo per dirlo |
| D | Un membro nel `body` | Niente colonna | Il corpo è degli schemi e arriva al pubblico con la versione; cambierebbe il confronto che legge chi approva |

## Da portare nel piano

- **§9.3** (modello dei contenuti): `cms_contents.draft_from_file`, «la bozza viene da un file»: la scrive il salvataggio che la nomina, un salvataggio che non la nomina non la tocca, la toglie la pubblicazione nella stessa transazione; è fuori dall'audit.
- **§16** (meccanismi generici): `[NotAudited]` tiene una colonna fuori anche dai nomi che l'autosalvataggio scrive nell'audit, non solo dalle copie.
- **La riga di §9.3 sull'import** (nota del 30 ottobre, «Da portare nel piano»): la riga della versione parte dalle parole dell'import perché lo dice la riga, per chiunque pubblichi.
