# Documentazione Tecnica - PokerFace (Poker Planning App)

## Panoramica dell'Architettura
Il progetto è suddiviso in tre moduli principali che collaborano per offrire un'esperienza real-time:
1. **PokerFace.Api**: Il Backend sviluppato in ASP.NET Core, che si compone di un layer Controller RESTful e un layer SignalR Hub per la sincronizzazione istantanea.
2. **PokerFace.Client**: Applicazione Frontend nativa basata su Blazor, responsabile della renderizzazione dell'interfaccia, della gestione dello stato utente e dell'aggancio WebSocket (SignalR).
3. **PokerFace.Shared**: Libreria condivisa che definisce i modelli di trasferimento dati (DTO), garantendo il disaccoppiamento ma limitando la duplicazione del codice.

Di seguito vengono analizzati nel dettaglio i componenti principali e il funzionamento dei vari metodi.

---

## 1. Backend (PokerFace.Api)

### 1.1 `TableService` (e `ITableService`)
Questa classe è il cuore della logica di business e funge da *In-Memory Data Store* (tramite l'uso di una `ConcurrentDictionary<Guid, Table>`). Non utilizzando un database relazionale, tutte le informazioni vengono processate rapidamente in RAM.

**Metodi esposti per il business:**
- **`CreateTable(CreateTableRequest request)`**: Inizializza un nuovo tavolo generando due stringhe token crittografiche: un token per l'accesso generale, e un `ModeratorToken` per garantire permessi speciali al creatore della stanza. Restituisce il DTO con le credenziali del tavolo.
- **`GetTable(Guid tableId)`**: Effettua l'accesso concorrenziale per recuperare il riferimento a un tavolo specifico.
- **`JoinTable(Guid tableId, JoinTableRequest request)`**: Inserisce un nuovo utente (`Participant`) tra gli array al tavolo. Crea per lui un token univoco di sessione, a patto che il tavolo non sia segnato come `IsDeleted`.
- **`ValidateParticipant(Guid tableId, string token, out Participant participant)`**: È il metodo delegato all'autorizzazione utente. Consente alle richieste API di identificarsi ricevendo un token in input, valutandolo associato ad uno specifico partecipante e aggiornando contestualmente l'informazione di `LastHeartbeat` per indicare che l'utente è "vivo" (online).
- **`ValidateModerator(Guid tableId, string token)`**: Accerta che il chiamante stia godendo dei diritti da moderatore. Controlla sia il token di moderazione globale del tavolo, che i permessi specifici del partecipante.
- **`StartSession(Guid tableId, string? topic)`**: Consente ad un moderatore di creare un nuovo round di votazione (Session). Genera l'id della sessione, le assegna un punteggio numerico incrementale temporale, bloccando ulteriori partenze finché questa non verrà chiusa.
- **`Vote(Guid tableId, Guid participantId, string value)`**: Permette ad un utente di votare. Estrae la sessione marcata come attiva dal tavolo. Se un utente ha già votato, sovrascrive il suo voto col nuovo, altrimenti aggiunge il nuovo record di voto all'array.
- **`EndSession(Guid tableId)`**: Imposta `IsActive = false` alla sessione corrente e appone una `EndedAt` date. È da questo switch che dipende la logica visiva di rivelamento carte sul client: i DTO smetteranno di mascherare i voti al momento del read.
- **`UpdateParticipantConnection`**: Usato per mappare e collegare lo stato del WebSocket SignalR di un utente sul suo modello di backend (impostando `DisconnectedAt` e rimuovendolo qualora ritorni).
- **`RemoveParticipant`**: Effettua l'effettiva rimozione di un utente del tavolo. Se questo era l'unico moderatore, ha una logica interna per passare il testimone assegnando in modo pseudocasuale il ruolo (`IsModerator`) a un utente rimanente.
- **`CleanupEmptyTables()`**: Metodo di Garbage Collection interno al programma. Scansiona parallelamente i tavoli e spazza via dalla memoria le stanze dove i partecipanti risiedono in uno stato disconnesso prolungato o pari a 0.

### 1.2 `TablesController` (Controller REST)
È il punto di ingresso HTTP del Backend. Convalida gli header e dialoga con il `TableService`.

**Metodi implementanti:**
- **`CreateTable / JoinTable / GetTable`**: Si comportano come da Proxy diretti chiamando il service ritornandone le Response avvolte in formato JSON (es: `CreatedAtAction`, `Ok`). In aggiunta, gli snippet legati agli inserimenti attivano lo push verso il Socket (es: `_hubContext.Clients.Group...` inviando l'evento `UserJoined`).
- **`UpdateParticipant / Vote / StartSession / EndSession`**: Espongono logica di modifica che richiede autorizzazione. Per tutti questi endpoint l'attributo header HTTP `[FromHeader(Name = "X-Participant-Token")]` viene estratto e iniettato ai metodi `Validate*` del server come meccanismo di verifica di identità sicura e stateless per permettere di invocare le modifiche. Se il controllo fallisce, restituiscono `401 Unauthorized` o `403 Forbid`.
- **`CleanupTables`**: Endpoint POST esposto come webhook per pulizia controllata a distanza della dictionary nel Service.

### 1.3 `PokerHub` (SignalR)
Si occupa della messaggistica fluida push server-to-client in protocollo WebSocket. L'hub non detiene uno state HTTP session di per sé, ma usa il costrutto nativo `Groups` di ASP.NET.

**Metodi principali e Override:**
- **`JoinTableGroup(Guid tableId, Guid participantId)`**: Inscrive forzatamente l'id della connessione dell'utente in un `Group` identificato dal guid del Tavolo (come in una 'stanza'). Salva nello scope della singola connessione dictionary interna i due item `TableId` e `ParticipantId`. Fatto ciò, genera in output sul gruppo condiviso in broadcast l'avvenuta connessione.
- **`OnDisconnectedAsync(Exception? exception)`**: Metodo override preconfigurato che il framework SignalR esegue istantaneamente al kill del browser client. L'applicazione sfrutta il passaggio locale per disconnettere virtualmente il partecipante. Avvia in modo asincrono un tempo d'attesa (3 secondi tramite `Task.Delay`); se una successiva riconnessione non corregge lo stato dell'utente ricollegandolo, viene richiamata l'infrastruttura di espulsione del tavolo, e la stanza notificata dei cambiamenti.

---

## 2. Frontend (PokerFace.Client)

### 2.1 `PokerService`
Classe Singleton / Servizio registrato su Blazor che astrae la classe nativa `HttpClient` incapsulando la state-machine dell'utente nel client. Mantiene in locale le chiavi di autenticazione in real-time.

**Metodi principali:**
- **`SetTokens(...)`**: Imposta ed incapsula lo stato di autorizzazione in read-only props una volta generate (Token utente, moderatore, e Guid attuali).
- **`CreateTable / JoinTable / GetTable`**: Raccoglie i model format dal client, esegue le richieste standard (POST e GET), ed in caso operazione completata ripopola i model richiamando automaticamente `SetTokens`.
- **Chiamate di business (es: `Vote`, `StartSession`, `EndSession`, `UpdateParticipant`)**: Usano un `HttpRequestMessage` Custom per inserire in maniera dinamica nei payload delle richieste la collection di header desiderata al Controller API (ossia iniettando `"X-Participant-Token"`).

### 2.2 `PokerRealTimeService`
Wrapper dedicato per gestire l'agile connettività di Blazor all'`HubConnection` della SDK SignalR, senza far sporcare il View layer dell'interfaccia UI con costrutti del Socket. Mette a disposizione i delegati / eventi `.NET event Action` standard per l'aggancio da altri componenti.

**Metodi implementati:**
- **`Connect(string hubUrl, Guid tableId, Guid participantId)`**: Costruisce assieme i parametri (Url e credenziali), chiude selettivamente le connettività in sospeso instanziando un nuovo `HubConnectionBuilder()`. Configura un pool di mapping per dirigere i Socket Event verso Delegate Classici C# (es. `_hubConnection.On<...>("UserVoted", ...)` --> `OnUserVoted?.Invoke(...)`).
- **Configurazione di Fault-Tolerance**: Instanzia una procedura anonima sull'evento nativo `Reconnected` che in caso di cadute temporanee e rimonta di connessione, re-inoltri automaticamente l'ingresso al `JoinTableGroup` verso i server, garantendo il rientro silente nella stanza nascondendo le cadute minori al DOM e l'integrità strutturale all'utente.
- **`DisconnectAsync` / `ReconnectAsync`**: Permettono procedure life-cycle custom o salvataggi di pulizia controllata del circuit di host da Blazor.

### 2.3 `Table.razor` (UI Component / Page)
Il punto di incontro reattivo, basato su `@rendermode InteractiveServer`. Implementa sia rendering visuale via states interni che interazione utente asincrona iterando la pipeline degli handlers.

**Componentistica e Metodi principali:**
- **`OnInitializedAsync()`**: Eseguito durante l'innesco in memoria. Verifica la presenza di token di appartenenza al tavolo (`PokerService.CurrentTableId`). In caso vi siano già, prosegue al restore (`LoadTable`), altrimenti predispone la booleana a renderizzare lo schema d'ingresso per presentare il proprio DisplayName per un `HandleJoin`.
- **`LoadTable()`**: Scarica una REST Snapshot veritiera dei model della partita e prosegue. Esegue i link degli `Action delegate` del SignalR per iniettare l'Update asincrono nel componente (`RealTime.OnUserVoted -= ... ; RealTime.OnUserVoted += HandleUserVoted;`). Al termine del mapping, apre il canale Web Socket effettivo lanciando asincronamente `RealTime.Connect()`.
- **Callbacks SignalR (es: `HandleSessionStarted`, `HandleUserVoted`, `HandleUserDisconnected`)**: Quando il controller richiama i push via Socket, questi trigger asincroni (spesso wrappati e passati al dispatcher con `InvokeAsync` per integrarsi in Blazor) cambiano le property della classe (`session = s;`, `table.Participants..IsConnected = false;`), e forzano Blazor ad un re-Render della UI chiamando esplicitamente `StateHasChanged()`; questo aziona l'immediato cambio per l'utente, come girare la carta o disattivare il dot di connessione.
- **`RunCountdown()`**: Orchestratore temporaneo UI basato sulla macchina a stati per mostrare i label transitori. Crea una stringa timer interattiva in loop di delay (`Task.Delay(1000)`) rendendo il re-flusso reattivo in attesa visuale per 3 secondi completandolo visualmente sull'utente finale prima di passare all'evento finale collegato (svelare le carte).
- **Bottoni Azioni Utente (`CastVote`, `StartSession`, `EndSession`)**: Non azionano lo stato locale per renderizzare (le carte girano unicamente quando arriva l'ok globale dal server verso il client). Funzionano triggerando istantaneamente le chiamate dirette al backend in ottica "fire-and-forget", attendendo l'OK dalla risorsa REST che farà auto-richiamare dai server il Socket, riallineando simultaneamente l'UI su ogni Browser aperto ad un dato tavolo.

### 2.4 Rendering Visivo e Animazioni (UI/UX)
L'interfaccia utente del tavolo da poker fa un uso intensivo di classi di utility Tailwind CSS e calcoli matematici a runtime per simulare un'esperienza di gioco reale.

- **Generazione del Tavolo**: Il tavolo centrale è renderizzato stilizzandolo con sfumature (es: un gradiente custom `poker-table-gradient`) e bordi fortemente arrotondati (`rounded-[100px] md:rounded-[200px]`) forzato in un rapporto fisso (`aspect-[2/1]`) per emulare perfettamente la forma di un tavolo ellittico da poker.
- **Seduta Equidistante dei Giocatori (`GetSeatingPosition`)**: Il posizionamento dinamico dei partecipanti (esclusi gli observer) attorno al tavolo aggira il normale DOM flow HTML ed è calcolato runtime in C# tramite coordinate css assolute (`left` e `top`). Il metodo C# sfrutta le formule della trigonometria (`Math.Cos` per le X e `Math.Sin` per le Y) per distribuire uniformemente un numero `N` di giocatori lungo il perimetro di un'ellisse virtuale. Calcolando lo step angolare rispetto al totale (`2 * Math.PI / totalCount`), ogni partecipante riceve le coordinate percentuali esatte per centrandosi poi grazie a `transform: translate(-50%, -50%)`.
- **Animazioni e Feedback Carte**: Quando un utente conferma il proprio voto, l'interfaccia reagisce rimpiazzando in SVG il segnaposto vuoto del giocatore con il retro stilizzato di una carta da poker. A quel div viene incollata dinamicamente la keyframe class `animate-bounce` di Tailwind: questo genera un effetto per il quale la carta "saltella" di continuo in loop sull'UI, garantendo un'immediata notifica visiva passiva che l'utente ha preso una decisione (mantenendo oscurato il voto reale in accordo alla logica delle sessioni).

--- 
***Nota Tecnica Architetturale***: Il sistema è stato disegnato senza l'ausilio di un Database permanente o file fisici persistenti per favorire una totale velocità e reattività "Real-Time" nei processi con una `ConcurrentDictionary`. Tutto risiede operativamente nel framework in tempo reale tra la RAM in pool del server, le sessioni Web Socket e il Client dell'utente.
