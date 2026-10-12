# Un solo lettore dei nomi (E10m)

**Data:** 11 ottobre 2026 — fase E10m di M4, PR del nucleo (#252)
**Stato:** **decisa** (Carmine, 11 ottobre 2026, pubblicata dal master sull'issue #247 su sua istruzione: [risposta][a247]): **sì**,
dalberone scrive nel nucleo **un solo lettore dei nomi per VID**, in una fase del nucleo di M4 con la sua PR e una nota nuova; **la
copia del nucleo (`ContactThreads`) passa sopra di lui nella stessa PR**, con test che tengono ciò che rispondono oggi i quattro
lettori; **la formazione e gli eventi passano dopo**, in piccole fasi di dalberone; **i tour in una sessione di Carmine**, come per
`ContractVersion`. Viene dalla #240, punto 4 delle [risposte di Carmine][a240]: «un lettore di nomi per VID nel nucleo entra nella coda
del nucleo, prima che se ne scriva un quinto». La forma nel codice (§3) è **scelta tecnica**; **una domanda** a Carmine (§6), nel corpo
della PR.
**Regola applicata:** `CLAUDE.md` §2 («a piece used in two places is written once») e §5, caso **(b)**: quattro copie dello stesso
pezzo, una nel nucleo e tre nei moduli, diventano un pezzo del nucleo. È una PR del nucleo, prima dei passaggi dei moduli
(`CLAUDE.md` §0 regola 6), come E10e per la distanza fra due aeroporti e E10g per la versione di un contratto.

## 1. Che cosa serve

Una pagina dello staff nomina le persone per VID: chi ha risposto in un filo dei contatti, il pilota e il validatore di un PIREP, il
trainee e il trainer di un training, chi ha prenotato uno slot. Il nome non si copia mai su una riga del modulo (piano §16.13, design
M3 §6, design M4 §1.13): si legge dai membri del nucleo, `hub_users`, quando serve. Oggi lo leggono **quattro copie**, ognuna con le sue
parole; una quinta sarebbe la prossima fase che nomina qualcuno.

## 2. Le quattro copie, lette nel codice (`main` a `bea8c0e`)

| | Nucleo: `ContactThreads.NamesAsync` (`Core/Content/ContactThreads.cs`) | Tour: `PirepReview.NamesAsync` (`Modules.FlightOps/Review/PirepReview.cs`) | Formazione: `TrainingPeople.NamesAsync` (`Modules.Training/TrainingPeople.cs`) | Eventi: `EventsPeople.NamesAsync` (`Modules.Events/EventsPeople.cs`) |
|---|---|---|---|---|
| prende | `IEnumerable<int>` | `IEnumerable<int?>` | `IEnumerable<int?>` | `IEnumerable<int>` |
| un VID nullo | — | lasciato fuori (`OfType<int>`) | lasciato fuori (`OfType<int>`) | — |
| un VID sotto uno | chiesto al database | **lasciato fuori prima** (`vid > 0`) | chiesto al database | chiesto al database |
| nessuno da chiedere | chiede al database, con una lista vuota | chiede al database, con una lista vuota | **non chiede** | **non chiede** |
| legge | la riga intera di `HubUser` | VID, nome e cognome | VID, nome e cognome | VID, nome e cognome |
| il nome | `$"{FirstName} {LastName}".Trim()`, in .NET | lo stesso | lo stesso | lo stesso |
| un nome vuoto | tenuto, `""` | tenuto, `""` | tenuto, `""` | tenuto, `""` |
| chi l'hub non conosce | lasciato fuori | lasciato fuori | lasciato fuori | lasciato fuori |
| risponde | `Dictionary<int, string>` | `IReadOnlyDictionary<int, string>` | `IReadOnlyDictionary<int, string>` | `IReadOnlyDictionary<int, string>` |

**Che cosa rispondono è lo stesso** in ogni stato che l'hub scrive: il nome e il cognome uniti da uno spazio e tagliati ai bordi, in
.NET (il `TRIM` di MariaDB toglie solo gli spazi, il `Trim` di .NET ogni bianco); un nome vuoto resta vuoto; chi non c'è manca. Una
persona cancellata è un VID negativo, e il suo utente se n'è andato con la cancellazione (`PersonalDataErasure.EraseCoreAsync`): nessuna
copia la nomina. **Le differenze sono tre**, e nessuna cambia una risposta che una pagina legge oggi:

1. **un VID sotto uno**: i tour lo lasciano fuori prima di chiedere, gli altri lo chiedono. La risposta cambia solo se in `hub_users`
   ci fosse una riga con un VID zero o negativo, che l'hub non scrive mai (un VID di IVAO è positivo, e la cancellazione toglie
   l'utente);
2. **nessuno da chiedere**: formazione ed eventi non vanno al database, nucleo e tour sì, con una lista vuota. La risposta è vuota in
   tutti e quattro;
3. **un VID nullo**: i tour e la formazione prendono anche le colonne che possono non nominare nessuno, e lasciano fuori il nullo come
   chi non c'è.

Più due differenze di forma, non di risposta: il nucleo legge la riga intera invece di tre colonne, e risponde con un dizionario
modificabile.

**Dopo il lettore, ogni modulo dice a modo suo che cosa è un nome vuoto**, e continua a dirlo: i tour lo chiamano nessun nome
(`PirepReview.Member`: `""` diventa `null`); la mail della formazione scrive il VID da solo (`TrainingPeople.Label`); la pagina della
formazione e quella degli eventi ricevono `""` (`TrainingPeople.Member`, `EventsPeople.Member`), e il `personName` del nucleo nel
browser scrive il VID. Per questo il lettore **tiene** il nome vuoto: è il punto in cui le pagine si separano, e resta loro.

## 3. La forma nel codice (scelta tecnica)

- **`src/IvaoHub.Core/People/MemberNames.cs`**, namespace `IvaoHub.Core.People`, una cartella nuova del nucleo: non è
  dell'accesso (`Core/Auth/`, chi entra e che cosa può), non della cancellazione (`Core/Privacy/`, che il lettore rispetta ma non fa),
  non dei contenuti (`Core/Content/`, dove stava la copia dei fili). È la persona come la conosce l'hub, e la prossima cosa che ne
  leggerà qualcosa per VID ha la sua cartella. Non nomina IVAO.
- **Una classe statica, due metodi**, come `GreatCircle` è una funzione statica:

  ```csharp
  public static class MemberNames
  {
      public static Task<IReadOnlyDictionary<int, string>> ReadAsync(HubDbContext hub, IEnumerable<int> vids, CancellationToken cancellationToken);
      public static Task<IReadOnlyDictionary<int, string>> ReadAsync(HubDbContext hub, IEnumerable<int?> vids, CancellationToken cancellationToken);
  }
  ```

  Il contesto lo passa chi chiede: le quattro copie hanno già un `HubDbContext` iniettato, e il passaggio di ognuna è il corpo del suo
  metodo che diventa una riga. Nessuna registrazione nel contenitore, nessuna riga in `Program.cs`, nessuna migrazione, nessun
  endpoint, nessuna chiave, niente nel browser.
- **Che cosa risponde**: le tre differenze di §2 sono decise **come le decideva la copia più stretta**, e nessuna cambia una risposta
  di oggi:
  1. **un VID sotto uno non ha mai un nome**, come nei tour, anche se una riga in `hub_users` lo avesse: lo dice la decisione sulla
     #247 («an erased person, a negative VID, is answered with no name»), ed è la regola di `isErased` delle pagine e del rifiuto della
     cancellazione (`vid <= 0`). Lo zero con loro: non è il VID di nessuno;
  2. **nessuno da chiedere non va al database**, come formazione ed eventi;
  3. **un VID nullo è lasciato fuori**, con il secondo metodo.
  Il resto è quello delle quattro: nome e cognome uniti e tagliati in .NET dopo la lettura, il nome vuoto tenuto, chi non c'è fuori,
  tre colonne lette, `IReadOnlyDictionary`.
- **`ContactThreads` passa sopra di lui** (`ReadAsync`, riga 188 ora 188–190): il suo metodo privato `NamesAsync` se ne va. Il lettore
  che è il mittente continua a non chiedere nessun nome (un dizionario vuoto, come il `[]` di prima). Che cosa legge un filo non cambia:
  gli autori delle risposte sono VID di chi ha scritto, o lo pseudonimo di chi è stato cancellato (`PersonColumns`), e uno pseudonimo
  non aveva un nome prima e non lo ha ora.

## 4. I test

`tests/IvaoHub.IntegrationTests/MemberNamesTests.cs`, nuovo, 7 casi, sul MariaDB vero. Le persone sono quelle dei fili dei contatti
(670011–670014), che `ContactThreadTests` rinomina ogni volta; i nomi che avevano tornano alla fine. Chi l'hub non conosce è
`int.MaxValue`, e la riga sotto uno è `-2000000000`, molto sotto ogni pseudonimo, tolta alla fine.

- **il lettore da solo**: nome e cognome uniti e tagliati, i due spazi in mezzo tenuti, un cognome vuoto, chi non c'è fuori, un VID
  chiesto due volte;
- **il test gemello**: il lettore e le tre copie dei moduli (`PirepReview`, `TrainingPeople`, `EventsPeople`) sugli stessi VID — un nome,
  un nome vuoto, un nome con un tab e uno spazio non separabile ai bordi, un cognome vuoto, chi non c'è, un nullo, un doppione; e una
  lista vuota —: la stessa risposta. La copia del nucleo se n'è andata con il passaggio; il nome che dava lo legge ancora
  `ContactThreadTests` («Test User» per lo staff, nessun nome per il mittente);
- **il nome vuoto tenuto**, e ogni modulo che lo legge ancora a modo suo dalla risposta del nucleo (§2);
- **un bianco che non è uno spazio, ai bordi, tagliato**: il `Trim` di .NET, non il `TRIM` del database;
- **una per ogni differenza** di §2: un VID sotto uno mai nominato, anche con una riga che lo ha (e che cosa rispondono oggi i tour, la
  formazione e gli eventi in quello stato); nessuno da chiedere che non arriva al database (un contesto già chiuso risponde vuoto, e
  chiesto di qualcuno rifiuta); un VID nullo lasciato fuori.

**Le prove al contrario** (fatte, poi rimesse; il lettore rotto, i sette casi rieseguiti): sono nel «Com'è andata» di E10m
(`10-piano-implementazione-m4.md`), con i casi che cadono per ognuna.

## 5. I passaggi dei moduli, dopo questa PR

Nessuno è in questa PR (`CLAUDE.md` §0 regole 2 e 6). Ognuno è il corpo di un metodo che diventa una riga, e **nessuna risposta
cambia** in uno stato che l'hub scrive (il test gemello):

- **la formazione** (una piccola fase di dalberone): `TrainingPeople.NamesAsync` risponde `MemberNames.ReadAsync(hub, vids,
  cancellationToken)`, oppure se ne va e i chiamanti chiedono al nucleo; `Member`, `Label` e `IsErased` restano. In `MemberNamesTests`
  esce la riga della formazione dal test gemello e dal test del VID sotto uno (dopo il passaggio, la formazione non nomina più quella
  riga: è lo stato che l'hub non scrive);
- **gli eventi** (una piccola fase di dalberone): lo stesso con `EventsPeople.NamesAsync`; `EventMemberDto` e `Member` restano;
- **i tour** (una sessione di Carmine): `PirepReview.NamesAsync` lo stesso; `Member` resta, con il suo nome vuoto che diventa `null`.
  In `MemberNamesTests` esce la riga dei tour.

Gli altri lettori di un nome nell'hub **non sono fra i quattro** e restano come sono, perché leggono altro insieme al nome o lo leggono
in un altro modo: l'anteprima della cancellazione (`PersonalDataErasure.PreviewAsync`, una persona), il trainee con rating e ore
(`TrainingRequests.TraineeAsync`), lo staff della formazione con rating e postazioni (`StaffTrainings`), l'elenco dello staff del blocco
(`CoreDataBlockProviders`, con le postazioni) e due letture di una persona nei contenuti, chi ha pubblicato e chi ha segnato pronto
(`ContentEndpoints`, `ContentReviewService`), che uniscono nome e cognome **nella query e senza tagliare**. Passare queste ultime due
cambierebbe ciò che rispondono (lo spazio in fondo di un cognome vuoto): non è di questa decisione.

## 6. La domanda a Carmine

**Una riga nella tabella di `CLAUDE.md` §2**, accanto a «The distance between two airports»: il nome di una persona per VID è
`MemberNames.ReadAsync` del nucleo (`Core/People/`), mai una copia. È la tabella dove la prossima sessione che nomina qualcuno lo cerca,
e il «prima che se ne scriva un quinto» della #240 lo tiene lì. Il file è suo, e la riga l'aggiunge il master. Raccomandazione: **sì**,
come per `GreatCircle` e `ContractVersion`. La domanda è nel corpo della PR.

## 7. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Un servizio nel contenitore (`MemberNames(HubDbContext)`, scoped) | una registrazione e un parametro di costruttore in più in ogni chiamante, che il `HubDbContext` lo ha già; nessuno stato da tenere |
| Un metodo su `HubDbContext` | il contesto è lo schema del nucleo, non le sue letture; e ogni modulo lo vedrebbe come una proprietà del database |
| Il nome unito nella query (`FirstName + " " + LastName`, `TRIM`) | il `TRIM` di MariaDB toglie solo gli spazi: un tab o uno spazio non separabile ai bordi resterebbe, e le quattro copie lo tagliano (§4) |
| Il nome vuoto come nessun nome (`null`) | è la scelta dei tour, non delle altre tre: la mail della formazione e il `personName` del browser lo leggono già a modo loro (§2) |
| Un VID sotto uno chiesto al database, come tre copie | la decisione sulla #247 vuole che una persona cancellata non abbia nome, e un VID sotto uno non è di nessuno: la regola sta nel lettore, non in una riga che manca |
| Un `IsErased` del nucleo in C# accanto al lettore | non serve al passaggio: la formazione ha il suo, gli eventi e le notifiche dicono `vid > 0`. Se un giorno serve a due moduli, è la sua decisione |
| Passare in questa PR anche la formazione e gli eventi | `CLAUDE.md` §0 regola 6: la modifica del nucleo prima, da sola, e i moduli dopo (la decisione sulla #247) |
| Passare anche i lettori di §5 che non sono fra i quattro | leggono altro, o rispondono altro: cambierebbero una risposta, fuori dalla decisione |

## Da portare nel piano

- `00-piano-di-progettazione.md` §16 (meccanismi generici), accanto alla persona cancellata e alla distanza fra due aeroporti: **dall'11
  ott 2026** (M4, E10m) il nome di una persona per VID lo legge il nucleo, `MemberNames.ReadAsync` (`Core/People/`): nome e cognome dai
  membri, uniti e tagliati; chi l'hub non conosce fuori; un VID nullo fuori; un VID sotto uno, una persona cancellata, mai un nome; il
  nome vuoto tenuto, e che cosa vuol dire lo dice la pagina. Mai una copia. Versione e changelog.
- `00-piano-di-progettazione.md` §9.7 (contratti trasversali), dove si dice che un nome non si copia sulla riga: si legge con
  `MemberNames`.
- `07-design-m3.md` §6 e `09-design-m4.md` §1.13 (il nome letto dai membri): con il lettore del nucleo, quando le fasi della formazione e
  degli eventi l'avranno adottato (§5).
- `05-design-m2.md`, la revisione dei PIREP: lo stesso, quando la sessione di Carmine avrà passato i tour (§5).
- `10-piano-implementazione-m4.md`: E10m, scritta in questa PR.
- **Una riga nella tabella di `CLAUDE.md` §2**, se Carmine la vuole (§6), che aggiunge il master con il piano.

[a240]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/240#issuecomment-6083877158
[a247]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/247#issuecomment-6113460844
