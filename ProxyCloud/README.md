Trustless Proxy: The proxy allows the cloud to always be reachable even without a static IP. Trustless technology ensures that the encryption does not show the unencrypted contents even to the proxi who handles the data packets
Web project in pure static HTML, with JavaScript code for encrypted communication with the cloud server. The communication in order to skip any firewal and to communicate a server without static IP uses a proprietary proxy software.
It is necessary to launch the proxy software before using the API and then reach the Server.
In this repository you will find:
The stand alone web application (a single file, static, with no external dependencies, which adds an effective and sophisticated encryption to the JSON REST API technology, which otherwise would remain insecure as a web server would still see in full all the content that transits with the requests POST and GET).
Il proxy trustless, ovvero una nostra implementazione di tecnologia incentrata sulla privacy e sicurezza. Abbiamo aggiunto la criptazione al server web che fa da proxy affinche i dati che transitano su esso non siano visibili.
In our cloud server / client system, we have added a proxy to allow the cloud to always be reachable even if it has no static IP address or is hidden behind a firewall!
Our technology has implemented low-level encryption to the communication APIs, in this way the web server where the proxy is installed will never be able to see the data that passes through it: the system provides for an exchange of cryptographic keys between the client and the cloud , which takes place with the help of a QR code, the data that is encrypted directly in the stand-alone html page that acts as a client, can only be decrypted when it reaches its destination in the cloud, and vice versa.
The JavaScript HTML client achieves maximum security as it is a static file (doesn't need a server to work), has no external dependencies, and can be very well launched by compiling it on your desktop and opening it with the prowser (without it residing on a remote server).

The proxy is connected with encrypted APIs to clients (it acts as a hub for multiple clients), and connected to the cloud with encrypted socket protocol with digital signature of packets.

Any dependencies to encrypted messaging protocols that are omitted in this repository can be found under this account as independent open source publications, usable for other projects as well.

[client html] <---- encrypted API ----> [proxy] <---- encrypted socket ----> [cloud]

Between client and cloud, the encryption is continuous (the web proxy cannot see the data in the clear text, which instead happens in traditional proxies and standard API implementations)
