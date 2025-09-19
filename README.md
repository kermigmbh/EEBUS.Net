# EEBUS.Net

![EEBUSAdapter-ezgif com-apng-to-gif-converter](https://github.com/user-attachments/assets/8a7ae8bc-1972-4b58-8b90-8616c1e4f939)

## Das Ziel dieses Projektes

In erster Linie ist das Ziel dieses Projektes ein in C# geschriebenes Heim-Energiemanagmentsystem (HEMS) mit einem EEBUS Interface ausstatten zu können, so dass es mit einer EEBUS fähigen Steuerbox zusammenarbeiten kann. Hintergrund ist das in Deutschland geltende Energiewirtschaftsgesetz mit seinem §14a und der Möglichkeit damit Netzentgelte zu sparen, also die eigenen Stromkosten zu senken. Um dies nutzen zu können werden von den Messtellenbetreibern intelligente Stromzähler mit sogenannten Steuerboxen installiert, die mit diversen Geräten oder besser mit einem zentralen HEMS entweder per Relaissteuerung oder eben mittels EEBUS kommunizieren.

Da es von den Machern der EEBUS Spezifikation leider nie eine open-source Referenzimplemementierung gegeben hat und diese Spezifikation offenkundig frei von echter Praxiserfahrung geschrieben wurde :-((, gestaltet sich ein solcher Versuch schwerer als nötig. Das es eine technisch so schlechte Spezifikation geschafft hat in deutschen Gesetzen Einzug zu halten ist allerdings eine politische Meisterleistung.

Glücklicherweise gibt es jedoch eine in Go geschriebene sehr vollständige Implementierung von Andreas Linde [enbility](https://github.com/enbility), die im Unterschied zu anderen Quellen tatsächlich open-source ist. Und es gibt mit [EEBUS.Net](https://github.com/digitaltwinconsortium/EEBUS.Net) von Erich Barnstedt einen ersten Ansatz für eine Implementierung in C#. Beiden möchte ich hier ganz ausdrücklich danken. Ohne deren Arbeiten wäre das hier vorgestellte Projekt völlig unmöglich. 

Basierend auf den Beispielcodes in [controlbox](https://github.com/enbility/eebus-go/tree/dev/examples/controlbox) war es möglich einen [ControlBox Simulator](https://github.com/vollautomat/eebus-go/tree/simulators) zu schreiben, gegen den sich ein EEBUS Adapter entwickeln läßt. Ein Test mit realer Hardware war bislang leider nicht möglich, da es Steuerboxen für solche Tests nunmal nicht einfach zum Ausleihen gibt. 


## Aktueller Stand

Aktuell besteht das Projekt aus 3 Teilen. Im Mittelpunkt steht ein .NET Assembly, das ein kleines Framework beinhaltet, das zumindest die hier erforderlichen Aspekte einer EEBUS Kommunikation abdecken soll. Als Entwicklungsumgebung, aber auch als Beispiel für eine Einbindung in ein HEMS ist der zweite Teil gedacht, ein ASP.NET Server, der das genannte Framework nutzt und sowohl ein Web API, als auch einen Websocket Kanal für eine Benutzeroberfläche zur Verfügung stellt. Der dritte Teil ist solch ein Web-Frontend, geschrieben mit Vue.ts. In einer realen Anwendung würde das erst genannte Assembly eingebunden werden und die beiden anderen Teile können als Vorlage für eine eigene Implementierung verwendet werden.

Aktuell werden die Usecases `LPC` und `LPP` unterstützt. `MPC` und `MGCP` sollen noch folgen.


## Installation 

Da das Projekt als Entwicklungsumgebung für eigene Anwendungen gedacht ist, macht es keinen Sinn nur fertigen ausführbaren Code zu installieren. Vielmehr sollte dieses Projekt in Visual Studio geöffnet werden und die einzelnen Teile von dort aus gestartet werden.

Vor einem ersten Start müssen jedoch einige IP Adress und Port Einstellungen an die eigene Umgebung angepasst werden. Dazu sind die jeweiligen Zeilen in der Datei `appsettings.json` entsprechend anzupassen. Die `urls` Angabe nennt die Adressen und Portnummern unter denen der ASP.NET Server erreichbar ist und die `Host` und `Port` Angaben unter `Settings` `Device` nennen die URL unter der das lokale EEBUS Device, das ein `ControllableSystem` darstellt, zu finden sein wird. `Host` ist also typischerweise die eigene IP Adresse der Entwicklungsmaschine. Alle anderen `Device` Parameter sollten selbsterklärend sein. Die initialen Werte für die verschiedenen Usecases werden nach einem Verbindungsaufbau an die Steuerbox gemeldet.

Noch ein Wort zum SSL Zertifikat, das für eine EEBUS Kommunikation obligatorisch ist. Beim ersten Start wird ein solches erzeugt und im EEBUS.vue Verzeichnis abgelegt. Also im Wurzelverzeichnis der Server und Client Teile dieses Projektes, denn sowohl das erzeugte EEBUS Device, als auch der Web API Server und das Webfrontend verwenden dieses Zertifikat in seinen unterschiedlichen Darstellungen. Dass hierbei zusätzlich ein selbstsigniertes Root Zertifikat erzeugt wird war nur ein Versuch, der vermutlich ziemlich nutzlos ist. Anscheinend akzeptiert sowieso kein aktueller Browser solche Root Zertifikate, auch wenn sie im entsprechenden Zertifikatespeicher registriert sind. Man muss sich also trotzdem mit den lästigen Browsermeldungen herumschlagen, dass die angeforderte Verbindung nicht sicher sei. Der Name des erzeugten Zertifikates kann bei Bedarf geändert werden. Dies ist allerdings an zwei Stellen erforderlich. Einmal in der `appsettings.json` des Server Projektes (Parameter `Certificate`) und zum anderen in der `vite.config.ts` des Webfrontends in Zeile 15.

Zum Testen des EEBUS Adapter wäre natürlich eine reale Steuerbox von großem Nutzen. Wer über eine solche verfügt und Zeit und Lust hat damit dieses Projekt zu testen möge doch bitte so nett sein und etwaige Erfahrungen hier rückmelden => [Discussions](https://github.com/vollautomat/EEBUS.Net/discussions).

Als Ersatz für eine Steuerbox kann der [ControlBox Simulator](https://github.com/vollautomat/eebus-go/tree/simulators) verwendet werden, der auf dem EEBUS Stack von Andreas Linde basiert. 
