## 2.8.10 – dokončení FSM UI a průběhu workerů

- GUI fáze jsou mapovány z `TestPhase`; SEND již nepřepisuje fáze ručními čísly.
- Přidán panel stavů všech workerů W1…Wn.
- Přidán checklist aktuální zprávy: Fronta → Rate limit → SMTP → MIME → SEND → OK.
- Chyba nyní zobrazuje zprávu a místo posledního selhání.
- Log je omezen na posledních 5000 řádků, aby dlouhý test nevyčerpal GUI paměť.
- Opraven duplicitní inicializátor `Multiselect`.



## 2.8.10 – stavový engine

Verze 2.8.10 přidává formální stavový automat v Core (`TestPhase`, `MessageStep`, `TestStateMachine`). Runner reportuje stav testu a stav jednotlivé zprávy/workeru; GUI z těchto stavů pouze vykresluje fáze a aktuální worker.

Při paralelním běhu se zobrazuje například `W2/5 · zpráva #17 · SMTP SEND`. Při retry je stav `4xx / retry`. Přechody jsou centralizované a testované unit testy.

## Testování 2.8.10

Automatické testy nyní zahrnují RateLimiter a základní lifecycle SMTP connection poolu: reuse zdravého klienta, discard po chybě, cancellation čekajícího Rent a bezpečný Return po Dispose.

> Poznámka: v prostředí, kde není nainstalované .NET SDK, nelze testy skutečně spustit. Na Windows/.NET 8 spusťte `dotnet test` před release buildem.


## 2.8.10 – test pass additions

Added runner-level SMTP integration tests covering successful delivery, permanent 5xx failures without retry,
transient 4xx retry/reconnect behavior, and cancellation with partial results. Added an additional
RateLimiter regression test for cancellation of an earlier reservation.
