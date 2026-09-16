# Le impostazioni dei moduli, i grant del file applicati uno per uno, le schermate di staff di un modulo

**Data:** 16 settembre 2026 — fase T5 di M2
**Stato:** **decisa** (Carmine, 16 settembre 2026, due domande in apertura di T5; la terza parte è un'estensione di un meccanismo).
**Regola applicata:** `CLAUDE.md` §5: **(c)** per le impostazioni e per il seed (un meccanismo nuovo e il cambio di una regola
del piano), **(b)** per le rotte di staff di un modulo (si estende il manifest, non lo si aggira).

## 1. Che cosa mancava

Aprendo T5, tre cose che il piano e il design davano per esistenti non esistevano:

1. **Il seed di `positionGrants` era «una volta per installazione»**: una riga `positionGrants.seeded` con un contatore. Un'installazione
   già avviata — il DB di sviluppo di Carmine compreso, dove il seed era stato applicato con zero grant — non avrebbe **mai**
   ricevuto i grant del FOD aggiunti ora a `division.json`, e lo stesso sarebbe successo con training ed eventi.
2. **Le impostazioni di un modulo**: il design M2 §1.11 le mette «nella riga `flightops` di `hub_division_settings`, con validazione e
   un form generato», ma il nucleo non aveva un modo con cui un modulo le dichiari, le validi e le esponga.
3. **Le rotte di un modulo nella SPA** si montavano solo sotto il layout pubblico; le schermate `/staff/tours/...` vogliono il
   layout e la guardia dello staff.

## 2. Le decisioni

1. **Ogni grant del file si ricorda da solo.** La riga `positionGrants.seeded` tiene l'**impronta** di ogni grant applicato
   (dipartimento, livelli in ordine, permesso, dipartimento su cui vale, effetto). A ogni avvio si applica solo quello che non c'è:
   un grant aggiunto al file da un modulo arriva una volta; uno cancellato dalla schermata dei permessi resta cancellato. Una riga
   scritta prima (un numero) si legge come «nessuno»: nessuna installazione aveva grant di posizione prima dei tour. Nessuna
   migrazione. *Scartato*: lasciarlo com'era e cancellare a mano la riga sul DB di sviluppo — il problema tornava a ogni modulo.
2. **Le impostazioni sono un meccanismo del nucleo**: `IModule.Settings` restituisce un `ModuleSettingsDescriptor` (il tipo, i valori
   di partenza, il validatore, il permesso). Il nucleo le tiene in `hub_division_settings` alla chiave `modules.{key}.settings`
   (auditata come ogni riga di quella tabella), le serve a `GET`/`PUT /api/modules/{key}/settings` dietro quel permesso **sul
   dipartimento di base** del modulo (su qualunque dipartimento se il modulo non ne ha), e il codice del modulo le legge con
   `ModuleSettingsStore.GetAsync<T>`. Un'impostazione aggiunta in una release successiva parte dal suo valore di partenza anche
   dove le altre erano già state salvate. *Scartato*: una tabella `fo_settings` di una riga nel contesto del modulo — zero codice
   nel nucleo, ma ogni modulo avrebbe rifatto la sua, e training ed eventi ne avranno.
3. **Una rotta di un modulo dice dove sta**: `RouteDefinition` guadagna `area: 'public' | 'staff'`, `permission` (tenuto su un
   dipartimento qualunque, come le schermate del nucleo) e `validateSearch`. Il router monta quelle di staff sotto `_staff`, così la
   guardia dello staff gira prima e il layout è lo stesso.

## 3. Due cose piccole trovate strada facendo

- **Il selettore dei tipi di aereo** che T1 doveva lasciare non c'era: nasce `IAircraftTypeDirectory` nel nucleo (cerca per codice o
  modello, dice quali codici non sono tipi) e `GET /api/reference/aircraft-types?q=`. Il campo è una proposta **chiusa** del form
  generato, che chiede al server mentre si scrive: nessuna estensione del generatore.
- **I tipi di un gruppo** sono nel form una lista di oggetti `{ icao }`, perché il generatore ripete oggetti; la mutazione li
  trasforma nell'elenco di codici del contratto.

## 4. Che cosa si tocca

Piano 0.83 (§9.7 contratto `IModule`, §16), design M2 §1.5, §1.11, §7.2; `docs/FORKING.md`; `division.example.json`. Codice:
`PositionGrantSeeder`, `Core/Modules/ModuleSettings.cs`, `ModuleRegistry.BaseDepartmentOf(key)`, `Core/Ivao/AircraftTypeDirectory.cs`,
`web/src/shared/modules.ts` e `app/router.ts`.
