# Revisione della checklist §16.E su tutto il codice di M1

**Data:** 7 settembre 2026 — fase G12, task 5 del piano di implementazione.
**Perimetro letto:** 226 file `.cs` sotto `src/`, 210 file `.ts`/`.tsx` sotto `web/src/`, più
`config/`, `locales/`, `seed/`, le due suite Playwright e la documentazione pubblica.
**Metodo:** le domande del template di PR, una per una, verificate **sul codice**. Dove la risposta è
«no» c'è come è stata verificata; dove è «sì» c'è l'eccezione e il perché.

La revisione omologa di M0 è `2026-09-04-m0-review.md`, e questa si legge accanto a quella: le
domande sono le stesse, il perimetro è cresciuto di 107 file `.cs` e 100 `.ts`/`.tsx`.

---

## Le domande

### 1. Ho aggiunto una tabella `*_translations`?

**No.** Zero occorrenze della stringa in tutto il repository, come in M0. I sei campi tradotti che M1
aggiunge — etichetta di un link del menu, nome di una categoria, `alt` di un file, oggetto e corpo di
un messaggio — sono colonne JSON `Localized<T>` con il converter unico e la convenzione `_i18n`.

### 2. Ho aggiunto un authorization handler?

**No.** Una sola implementazione di `AuthorizationHandler<>` in tutto `src/`:
`Auth/Permissions/HubAuthorization.cs`. M1 ha aggiunto **sei permessi in tre aree** e **zero**
handler, che era la promessa di piano §16.3.

### 3. Ho scritto un `fetch` a mano?

**No.** Zero `fetch(` fuori da `web/src/shared/api/`, e la regola ESLint continua a impedirlo. Le
schermate nuove — media, categorie, calendario, contatti, menu, ricerca — passano tutte dal client
generato sul contratto OpenAPI.

### 4. Ho scritto una lista o un form non generati?

**No, e questa è la risposta che M0 non poteva ancora dare.** Ogni rotta di lista del back-office usa
la lista generica: calendario, categorie, contatti, contenuti, link, media, menu, permessi, audit,
moduli. Nessun `<table>` in tutto `web/src`, e l'unico `<form>` resta quello di `SchemaForm.tsx`.

I form sono generati dallo schema zod, **generatore compreso**: quando `allowedBlocks` ha chiesto una
cosa che il generatore non sapeva disegnare, si è esteso il generatore (`multi`, §1.6) invece di
scrivere il campo a mano. È la regola (b) applicata nel caso in cui era più facile aggirarla.

### 5. Ho aggiunto un componente UI fuori dall'elenco chiuso?

**Quattro, ed erano i quattro dichiarati**: `CalendarView`, `ContactForm`, `LiveStatusStrip`,
`MediaPicker` — esattamente i nomi di design §12, nessuno in più.

⚠️ Nota che vale per M2: i **27 blocchi** non hanno aggiunto un solo componente all'elenco. Un blocco
non è un componente, ed è la ragione per cui il volume di M1 è costato così poco.

Fuori dall'elenco condiviso ci sono tre pezzi che vivono **dentro** una schermata e non sono
riutilizzabili — il pannello delle differenze dal template, la riga della sezione bloccata e la
cornice dell'anteprima a tre larghezze, tutti in `features/content/`. Sono parte dell'editor, non
pezzi del kit, e non entrano nell'elenco.

### 6. Ho aggiunto una chiave esterna fra due contesti?

**No, e la regola non è ancora stata messa alla prova.** C'è **un solo** `DbContext`
(`HubDbContext`): l'unico modulo, `Atc`, non ha dati suoi. Le 44 chiavi esterne del progetto sono
tutte interne a quel contesto. La domanda diventerà vera con Events (M2), che è il primo modulo con
una tabella.

### 7. Ho fatto una chiamata SMTP diretta?

**No.** Un solo file nomina `SmtpClient`: `Notifications/MailSender.cs`, che **è** il servizio
notifiche del nucleo. I contatti — l'unica cosa di M1 che manda mail — pubblicano un intento e non
sanno niente di SMTP, code o tentativi.

### 8. Un modulo referenzia un altro modulo?

**No.** Zero import da `features/` verso `modules/` e zero fra moduli, verificati sui sorgenti; il
test `manifest.test.ts` li fissa. `IvaoHub.Modules.Atc` referenzia solo `Core`.

### 9. I test della spina dorsale passano ancora tutti?

**Sì.** Al momento della revisione: **456 test .NET** (300 unit + 156 integrazione contro una MariaDB
11.4.10 vera), **253 Vitest**, **42 smoke Playwright**, **12 del giro pieno**. Nessuno `Skip`.

Fra questi ci sono i test che tengono la spina dorsale: l'unicità dell'authorization handler
(per riflessione **e** sui sorgenti), l'interceptor che scrive audit e proiezioni nella stessa
transazione, `Localized<T>`, il global query filter, `IProjectable`, e il test di forkabilità della
divisione fittizia XX — che adesso passa **con il sito pubblico completo**, che è il punto 8 della
definizione di fatto.

### 10. Il cambio ha richiesto una decisione? Allora il piano ha una nuova versione e un changelog.

**Sì, nove volte**, e tutte e nove hanno una nota in `docs/internal/decisions/`: l'ambiente e2e, la
dashboard di dipartimento, i template di sistema e i dipartimenti, l'`alt` delle immagini,
l'autorizzazione su un pezzo di un altro dipartimento, l'indirizzo di un destinatario, una riga
scritta da fuori, le vIPI dentro l'hub, scrivere un template, e il loader che non è la riga.

Il design di M1 è passato da 1.0 a **1.15**, il piano di implementazione da 1.0 a **2.5**, ognuno con
la sua riga di changelog in testa.

### 11. Il conto delle tre righe

`decisions/2026-09-07-m1-review.md`, che è il posto dove i numeri stanno accanto alla previsione. In
breve: **6 tabelle** (previste 6), **3 aree di permessi** (previste 3), **4 componenti custom**
(previsti 4), **6 estensioni del generatore** (previste 5), **7 endpoint scritti a mano** (previsto
1) — e **zero CRUD scritti a mano**, che è la cosa che quel numero voleva davvero proteggere.

---

## Che cosa questa revisione ha trovato

Niente da correggere nel codice. Le due cose che valgono per M2:

1. ⚠️ **La domanda 6 non ha ancora un caso vero.** Un solo contesto vuol dire che «nessuna FK fra
   contesti» è finora una promessa, non una misura. Il primo modulo con una tabella la mette alla
   prova, e va guardata lì.
2. ⚠️ **La domanda 5 va riformulata come la 11.** «Un componente fuori dall'elenco» ha retto perché
   qualcuno ha contato i blocchi separatamente; la stessa distinzione — pezzo condiviso contro pezzo
   di una schermata — va scritta nel template di PR, altrimenti la si rifà a memoria ogni volta.
