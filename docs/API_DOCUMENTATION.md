# PokerFace API Documentation

API Backend per Planning Poker - Stima collaborativa dei task in stile planningpokeronline.

**Base URL:** `http://localhost:5198/api`

---

## Indice

1. [Autenticazione](#autenticazione)
2. [Tavoli (Tables)](#tavoli-tables)
3. [Partecipanti (Participants)](#partecipanti-participants)
4. [Sessioni di Voto (Voting Sessions)](#sessioni-di-voto-voting-sessions)
5. [Voti (Votes)](#voti-votes)
6. [Valori di Voto Validi](#valori-di-voto-validi)
7. [Codici di Errore](#codici-di-errore)

---

## Autenticazione

L'API non utilizza autenticazione tradizionale. Invece, ogni utente riceve un **token univoco** al momento della creazione o dell'ingresso in un tavolo.

### Header Richiesto

Per tutte le chiamate che richiedono autenticazione, includere:

```
X-Participant-Token: <token>
```

### Tipi di Token

| Token | Descrizione | Ottenuto da |
|-------|-------------|-------------|
| `participantToken` | Identifica l'utente, permette azioni base | Creazione tavolo o Join |
| `moderatorToken` | Permette azioni di gestione tavolo | Solo creazione tavolo |

---

## Tavoli (Tables)

### Crea Tavolo

Crea un nuovo tavolo da gioco e diventa il moderatore.

**Endpoint:** `POST /api/tables`

**Autenticazione:** Nessuna

**Request Body:**

| Campo | Tipo | Obbligatorio | Descrizione |
|-------|------|--------------|-------------|
| `tableName` | string | Sì | Nome del tavolo (max 100 caratteri) |
| `moderatorName` | string | Sì | Nome del moderatore (max 50 caratteri) |
| `isObserver` | boolean | No | Se `true`, il moderatore non può votare (default: `false`) |

**Esempio Request:**

```http
POST /api/tables
Content-Type: application/json

{
  "tableName": "Sprint Planning Q1",
  "moderatorName": "Scrum Master",
  "isObserver": false
}
```

**Esempio Response (201 Created):**

```json
{
  "tableId": "81e51a01-b4f3-446f-9b47-e43feccf978d",
  "tableName": "Sprint Planning Q1",
  "moderatorToken": "9OfXmlDOF6VBlmUiDYbECcJhJfW_p7HCfFMGGeHuraM",
  "participantToken": "Vq3p5Zfyq8yPCHqb9Kdz1yGxoDrvDrYEF4a5m_MsMLc",
  "participantId": "4c3584c3-eb30-47b4-b484-a0b68220d2b7"
}
```

> **Nota:** Salva entrambi i token! Il `moderatorToken` è necessario per gestire il tavolo, il `participantToken` per le azioni utente.

---

### Ottieni Stato Tavolo

Recupera lo stato corrente del tavolo, inclusi partecipanti e sessione di voto attiva.

**Endpoint:** `GET /api/tables/{tableId}`

**Autenticazione:** `X-Participant-Token` (partecipante)

**Path Parameters:**

| Parametro | Tipo | Descrizione |
|-----------|------|-------------|
| `tableId` | GUID | ID del tavolo |

**Esempio Request:**

```http
GET /api/tables/81e51a01-b4f3-446f-9b47-e43feccf978d
X-Participant-Token: Vq3p5Zfyq8yPCHqb9Kdz1yGxoDrvDrYEF4a5m_MsMLc
```

**Esempio Response (200 OK):**

```json
{
  "tableId": "81e51a01-b4f3-446f-9b47-e43feccf978d",
  "tableName": "Sprint Planning Q1",
  "isClosed": false,
  "createdAt": "2024-01-15T10:30:00Z",
  "participants": [
    {
      "id": "4c3584c3-eb30-47b4-b484-a0b68220d2b7",
      "displayName": "Scrum Master",
      "isModerator": true,
      "isObserver": false,
      "isConnected": true,
      "lastHeartbeat": "2024-01-15T10:35:00Z",
      "hasVoted": true
    },
    {
      "id": "83971885-6373-4a3b-9520-bdd49caed199",
      "displayName": "Developer 1",
      "isModerator": false,
      "isObserver": false,
      "isConnected": true,
      "lastHeartbeat": "2024-01-15T10:34:50Z",
      "hasVoted": false
    }
  ],
  "currentSession": {
    "id": "009eba4d-4876-4f83-8dbd-c9172950c9bf",
    "sessionNumber": 1,
    "topic": "User Story #42",
    "isActive": true,
    "startedAt": "2024-01-15T10:32:00Z",
    "endedAt": null,
    "votes": [
      {
        "participantId": "4c3584c3-eb30-47b4-b484-a0b68220d2b7",
        "participantName": "Scrum Master",
        "value": null
      }
    ]
  },
  "totalSessions": 1
}
```

> **Nota:** I valori dei voti (`value`) sono `null` mentre la sessione è attiva. Vengono rivelati solo dopo la chiusura della sessione.

---

### Chiudi Tavolo

Chiude il tavolo e disconnette tutti i partecipanti.

**Endpoint:** `DELETE /api/tables/{tableId}`

**Autenticazione:** `X-Participant-Token` (moderatore)

**Path Parameters:**

| Parametro | Tipo | Descrizione |
|-----------|------|-------------|
| `tableId` | GUID | ID del tavolo |

**Esempio Request:**

```http
DELETE /api/tables/81e51a01-b4f3-446f-9b47-e43feccf978d
X-Participant-Token: 9OfXmlDOF6VBlmUiDYbECcJhJfW_p7HCfFMGGeHuraM
```

**Response:** `204 No Content`

---

## Partecipanti (Participants)

### Unisciti al Tavolo

Permette a un utente di unirsi a un tavolo esistente.

**Endpoint:** `POST /api/tables/{tableId}/participants`

**Autenticazione:** Nessuna

**Path Parameters:**

| Parametro | Tipo | Descrizione |
|-----------|------|-------------|
| `tableId` | GUID | ID del tavolo |

**Request Body:**

| Campo | Tipo | Obbligatorio | Descrizione |
|-------|------|--------------|-------------|
| `displayName` | string | Sì | Nome visualizzato (max 50 caratteri) |
| `isObserver` | boolean | No | Se `true`, l'utente non può votare (default: `false`) |

**Esempio Request (Partecipante - può votare):**

```http
POST /api/tables/81e51a01-b4f3-446f-9b47-e43feccf978d/participants
Content-Type: application/json

{
  "displayName": "Developer 1",
  "isObserver": false
}
```

**Esempio Request (Osservatore - non può votare):**

```http
POST /api/tables/81e51a01-b4f3-446f-9b47-e43feccf978d/participants
Content-Type: application/json

{
  "displayName": "Product Owner",
  "isObserver": true
}
```

**Esempio Response (201 Created):**

```json
{
  "participantId": "83971885-6373-4a3b-9520-bdd49caed199",
  "token": "_0GN8hFzARh4dKwSk_X2xIZgQs8aJP1zlNgs_bD780g",
  "tableId": "81e51a01-b4f3-446f-9b47-e43feccf978d",
  "tableName": "Sprint Planning Q1",
  "isModerator": false
}
```

---

### Heartbeat (Keep-Alive)

Mantiene attiva la connessione dell'utente e recupera lo stato aggiornato del tavolo.

**Endpoint:** `POST /api/tables/{tableId}/participants/heartbeat`

**Autenticazione:** `X-Participant-Token` (partecipante)

**Frequenza Consigliata:** Ogni 30 secondi

**Timeout:** Un utente viene considerato disconnesso dopo 1 minuto senza heartbeat.

**Esempio Request:**

```http
POST /api/tables/81e51a01-b4f3-446f-9b47-e43feccf978d/participants/heartbeat
X-Participant-Token: Vq3p5Zfyq8yPCHqb9Kdz1yGxoDrvDrYEF4a5m_MsMLc
```

**Esempio Response (200 OK):**

```json
{
  "success": true,
  "serverTime": "2024-01-15T10:35:00Z",
  "tableState": {
    "tableId": "81e51a01-b4f3-446f-9b47-e43feccf978d",
    "tableName": "Sprint Planning Q1",
    "isClosed": false,
    "createdAt": "2024-01-15T10:30:00Z",
    "participants": [...],
    "currentSession": {...},
    "totalSessions": 1
  }
}
```

> **Nota:** Il heartbeat restituisce lo stato completo del tavolo, utile per il polling.

---

### Esci dal Tavolo

Permette all'utente di lasciare volontariamente il tavolo.

**Endpoint:** `DELETE /api/tables/{tableId}/participants/me`

**Autenticazione:** `X-Participant-Token` (partecipante)

**Esempio Request:**

```http
DELETE /api/tables/81e51a01-b4f3-446f-9b47-e43feccf978d/participants/me
X-Participant-Token: Vq3p5Zfyq8yPCHqb9Kdz1yGxoDrvDrYEF4a5m_MsMLc
```

**Response:** `204 No Content`

> **Nota:** Se tutti i partecipanti escono, il tavolo viene chiuso automaticamente.

---

## Sessioni di Voto (Voting Sessions)

### Avvia Sessione di Voto

Avvia una nuova sessione di voto. Solo il moderatore può farlo.

**Endpoint:** `POST /api/tables/{tableId}/sessions`

**Autenticazione:** `X-Participant-Token` (moderatore)

**Request Body:**

| Campo | Tipo | Obbligatorio | Descrizione |
|-------|------|--------------|-------------|
| `topic` | string | No | Descrizione del task da stimare (max 500 caratteri) |

**Esempio Request:**

```http
POST /api/tables/81e51a01-b4f3-446f-9b47-e43feccf978d/sessions
Content-Type: application/json
X-Participant-Token: 9OfXmlDOF6VBlmUiDYbECcJhJfW_p7HCfFMGGeHuraM

{
  "topic": "User Story #123 - Implementare login OAuth"
}
```

**Esempio Response (201 Created):**

```json
{
  "sessionId": "009eba4d-4876-4f83-8dbd-c9172950c9bf",
  "sessionNumber": 1,
  "topic": "User Story #123 - Implementare login OAuth",
  "startedAt": "2024-01-15T10:32:00Z"
}
```

> **Nota:** Non è possibile avviare una nuova sessione se ce n'è già una attiva.

---

### Ottieni Sessione Corrente

Recupera i dettagli della sessione di voto attiva.

**Endpoint:** `GET /api/tables/{tableId}/sessions/current`

**Autenticazione:** `X-Participant-Token` (partecipante)

**Esempio Request:**

```http
GET /api/tables/81e51a01-b4f3-446f-9b47-e43feccf978d/sessions/current
X-Participant-Token: Vq3p5Zfyq8yPCHqb9Kdz1yGxoDrvDrYEF4a5m_MsMLc
```

**Esempio Response (200 OK):**

```json
{
  "id": "009eba4d-4876-4f83-8dbd-c9172950c9bf",
  "sessionNumber": 1,
  "topic": "User Story #123 - Implementare login OAuth",
  "isActive": true,
  "startedAt": "2024-01-15T10:32:00Z",
  "endedAt": null,
  "votes": [
    {
      "participantId": "4c3584c3-eb30-47b4-b484-a0b68220d2b7",
      "participantName": "Scrum Master",
      "value": null
    },
    {
      "participantId": "83971885-6373-4a3b-9520-bdd49caed199",
      "participantName": "Developer 1",
      "value": null
    }
  ]
}
```

**Response (404 Not Found) se nessuna sessione attiva:**

```json
{
  "error": "No active session"
}
```

---

### Termina Sessione di Voto

Termina la sessione corrente e rivela i voti. Solo il moderatore può farlo.

**Endpoint:** `PUT /api/tables/{tableId}/sessions/current/end`

**Autenticazione:** `X-Participant-Token` (moderatore)

**Esempio Request:**

```http
PUT /api/tables/81e51a01-b4f3-446f-9b47-e43feccf978d/sessions/current/end
X-Participant-Token: 9OfXmlDOF6VBlmUiDYbECcJhJfW_p7HCfFMGGeHuraM
```

**Esempio Response (200 OK):**

```json
{
  "sessionId": "009eba4d-4876-4f83-8dbd-c9172950c9bf",
  "sessionNumber": 1,
  "endedAt": "2024-01-15T10:40:00Z",
  "results": [
    {
      "participantId": "4c3584c3-eb30-47b4-b484-a0b68220d2b7",
      "participantName": "Scrum Master",
      "value": "5"
    },
    {
      "participantId": "83971885-6373-4a3b-9520-bdd49caed199",
      "participantName": "Developer 1",
      "value": "8"
    }
  ]
}
```

---

## Voti (Votes)

### Vota

Registra o aggiorna il voto dell'utente nella sessione corrente.

**Endpoint:** `POST /api/tables/{tableId}/sessions/current/votes`

**Autenticazione:** `X-Participant-Token` (partecipante)

**Requisiti:**
- L'utente NON deve essere un osservatore (`isObserver: false`)
- Deve esistere una sessione di voto attiva

**Request Body:**

| Campo | Tipo | Obbligatorio | Descrizione |
|-------|------|--------------|-------------|
| `value` | string | Sì | Valore del voto (vedi [valori validi](#valori-di-voto-validi)) |

**Esempio Request:**

```http
POST /api/tables/81e51a01-b4f3-446f-9b47-e43feccf978d/sessions/current/votes
Content-Type: application/json
X-Participant-Token: Vq3p5Zfyq8yPCHqb9Kdz1yGxoDrvDrYEF4a5m_MsMLc

{
  "value": "5"
}
```

**Esempio Response (201 Created):**

```json
{
  "success": true,
  "voteId": "5939407d-958c-465c-93ea-c220e80bd9de",
  "value": "5",
  "castAt": "2024-01-15T10:35:00Z"
}
```

**Response (400 Bad Request) per valore non valido:**

```json
{
  "error": "Invalid vote value",
  "validValues": ["0", "1", "2", "3", "5", "8", "13", "21", "34", "55", "89", "?", "coffee"]
}
```

> **Nota:** Se l'utente ha già votato, il voto viene aggiornato con il nuovo valore.

---

## Valori di Voto Validi

| Valore | Descrizione |
|--------|-------------|
| `0` | Zero story points |
| `1` | 1 story point |
| `2` | 2 story points |
| `3` | 3 story points |
| `5` | 5 story points |
| `8` | 8 story points |
| `13` | 13 story points |
| `21` | 21 story points |
| `34` | 34 story points |
| `55` | 55 story points |
| `89` | 89 story points |
| `?` | Non so / Ho bisogno di più informazioni |
| `coffee` | Pausa caffè |

---

## Codici di Errore

| Codice | Descrizione |
|--------|-------------|
| `200 OK` | Richiesta completata con successo |
| `201 Created` | Risorsa creata con successo |
| `204 No Content` | Operazione completata (nessun contenuto da restituire) |
| `400 Bad Request` | Richiesta non valida (es. valore voto errato) |
| `401 Unauthorized` | Token mancante o non valido |
| `403 Forbidden` | Non autorizzato (es. non sei il moderatore) |
| `404 Not Found` | Risorsa non trovata (es. tavolo chiuso, nessuna sessione attiva) |
| `409 Conflict` | Conflitto (es. sessione già attiva) |

---

## Flusso Tipico di Utilizzo

```
1. Moderatore crea tavolo          → POST /api/tables
2. Moderatore condivide tableId    → (fuori dall'API)
3. Partecipanti si uniscono        → POST /api/tables/{id}/participants
4. Tutti inviano heartbeat         → POST /api/tables/{id}/participants/heartbeat (ogni 30s)
5. Moderatore avvia sessione       → POST /api/tables/{id}/sessions
6. Partecipanti votano             → POST /api/tables/{id}/sessions/current/votes
7. Moderatore termina sessione     → PUT /api/tables/{id}/sessions/current/end
8. (Ripetere dal punto 5 per altri task)
9. Moderatore chiude tavolo        → DELETE /api/tables/{id}
```

---

## Note Importanti

### Osservatori vs Partecipanti

- **Partecipante** (`isObserver: false`): Può votare nelle sessioni
- **Osservatore** (`isObserver: true`): Può vedere tutto ma NON può votare

Il ruolo viene scelto al momento del join e non può essere modificato.

### Keep-Alive e Disconnessione

- Inviare heartbeat ogni **30 secondi**
- Un utente viene disconnesso dopo **1 minuto** senza heartbeat
- Se tutti gli utenti si disconnettono, il tavolo viene chiuso automaticamente

### Sicurezza Token

- I token sono generati crittograficamente (256-bit)
- Ogni token è univoco e associato a un solo utente
- Non condividere il `moderatorToken` - dà controllo completo sul tavolo
