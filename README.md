<p align="center"><img src="docs/icone.png" width="96" alt="TrocaJunto"></p>

<h1 align="center">TrocaJunto</h1>

<p align="center">
  <b>O mouse Logitech acompanha o teclado quando você aperta 1, 2 ou 3 (Easy-Switch).</b><br>
  <i>The Logitech mouse follows the keyboard when you press its Easy-Switch keys 1, 2 or 3.</i>
</p>

<p align="center">
  <a href="https://github.com/PSFCODE/TrocaJunto/releases/latest/download/TrocaJunto.exe"><b>⬇ Baixar / Download TrocaJunto.exe</b></a>
  &nbsp;·&nbsp; Windows 10/11 (64 bits)
  &nbsp;·&nbsp; <a href="#english">English below</a>
</p>

---

## Português

Quem usa um teclado e um mouse Logitech com **Easy-Switch** em mais de um computador conhece o problema: você aperta o **2** no teclado, ele vai para o outro computador… e o mouse fica para trás, e você ainda precisa virar o mouse e apertar o botão de baixo.

O **TrocaJunto** resolve isso. Ele fica num ícone perto do relógio. Quando você aperta **1, 2 ou 3** no teclado, ele manda **o mouse e o teclado juntos** para o mesmo computador.

### Como instalar

1. **[Baixe o TrocaJunto.exe](https://github.com/PSFCODE/TrocaJunto/releases/latest/download/TrocaJunto.exe)**. Ele já vem com tudo dentro: não precisa instalar mais nada.
2. Coloque o arquivo numa pasta fixa, por exemplo `Documentos\TrocaJunto`. Não deixe em Downloads: o Windows abre ele dali toda vez que ligar.
3. Dê dois cliques. Se aparecer **"O Windows protegeu o computador"**, clique em **Mais informações → Executar assim mesmo**. O aviso aparece porque o programa é gratuito e não tem assinatura paga.
4. Aparece um ícone com duas setas douradas perto do relógio (pode estar escondido na setinha **^**). Ele já fica marcado para iniciar junto com o Windows.
5. **Repita em todos os computadores** que você usa com o teclado e o mouse.

### O que precisa

- Teclado e mouse Logitech com Easy-Switch, **ligados por Bluetooth**. O receptor USB (Unifying/Bolt) ainda não é suportado.
- **O mesmo número nos dois aparelhos** para cada computador. Por exemplo, se o notebook é o canal 1 no teclado, tem que ser o canal 1 no mouse também. Passe o mouse sobre o ícone para conferir: ele mostra "teclado no canal X · mouse no canal X".
- O TrocaJunto rodando **no computador de onde você está saindo**. Por isso, instale em todos.

| Aparelho | Situação |
|---|---|
| MX Keys + MX Master 3 | ✅ testado |
| MX Keys S, MX Keys Mini, MX Mechanical, MX Master 3S, MX Anywhere 3 / 3S | deve funcionar (mesmo protocolo), ainda não testado |

Testou com outro modelo? Conte numa [issue](https://github.com/PSFCODE/TrocaJunto/issues) se funcionou ou não.

### Bom saber

- **Mouse dormindo:** o MX Master dorme depois de um ou dois minutos parado. Se ele estiver dormindo na hora da troca, o teclado vai na hora e o mouse vai sozinho assim que você mexer nele (vale por até 30 minutos).
- **Logi Options+:** pode continuar instalado, os dois convivem. O *Flow* do Options+ faz o contrário (o teclado segue o mouse quando ele passa pela borda da tela).
- **Para parar:** clique com o botão direito no ícone e escolha **Sair**. As teclas 1-2-3 voltam a trocar só o teclado, como antes.
- **Se as teclas 1-2-3 pararem de responder** (por exemplo, se o programa travar): desligue e ligue o teclado pela chave do lado. Ele volta ao normal na hora.
- **Para desinstalar:** desmarque *Iniciar com o Windows*, escolha **Sair** e apague o arquivo.
- **Registro:** clique com o botão direito no ícone e escolha **Ver o registro**. Ajuda a entender o que aconteceu.

---

## English

If you use a Logitech keyboard and mouse with **Easy-Switch** on more than one computer, you know the drill: you press **2** on the keyboard, it jumps to the other computer… and the mouse stays behind, so you flip the mouse over and press its button too.

**TrocaJunto** ("switch together" in Portuguese) fixes that. It sits in the system tray. When you press **1, 2 or 3** on the keyboard, it sends **both the mouse and the keyboard** to the same computer.

### Install

1. **[Download TrocaJunto.exe](https://github.com/PSFCODE/TrocaJunto/releases/latest/download/TrocaJunto.exe)**. It is self-contained: nothing else to install.
2. Put it in a permanent folder, e.g. `Documents\TrocaJunto`. Not in Downloads: Windows starts it from there at every sign-in.
3. Double-click it. If **"Windows protected your PC"** shows up, click **More info → Run anyway**. That warning appears because this free app has no paid code-signing certificate.
4. A tray icon with two golden arrows appears (it may be hidden under the **^** arrow). It is already set to start with Windows.
5. **Repeat on every computer** you use with the keyboard and mouse.

### Requirements

- A Logitech keyboard and mouse with Easy-Switch, **connected over Bluetooth**. USB receivers (Unifying/Bolt) are not supported yet.
- **The same channel number on both devices** for each computer. For example, if your laptop is channel 1 on the keyboard, it must be channel 1 on the mouse too. Hover the tray icon to check: it shows "keyboard on channel X · mouse on channel X".
- TrocaJunto running **on the computer you are leaving**. That's why it goes on every computer.

| Devices | Status |
|---|---|
| MX Keys + MX Master 3 | ✅ tested |
| MX Keys S, MX Keys Mini, MX Mechanical, MX Master 3S, MX Anywhere 3 / 3S | should work (same protocol), not tested yet |

Tried it with another model? Tell us in an [issue](https://github.com/PSFCODE/TrocaJunto/issues) whether it worked.

### Good to know

- **Sleeping mouse:** the MX Master sleeps after a minute or two of inactivity. If it is asleep when you switch, the keyboard goes right away and the mouse follows as soon as you move it (for up to 30 minutes).
- **Logi Options+:** can stay installed; they get along. Options+ *Flow* does the opposite (the keyboard follows the mouse across screen edges).
- **To stop:** right-click the icon and choose **Exit**. Keys 1-2-3 go back to switching only the keyboard.
- **If keys 1-2-3 stop responding** (e.g. the app crashed): turn the keyboard off and on with its side switch. It goes back to normal immediately.
- **To uninstall:** untick *Start with Windows*, choose **Exit**, and delete the file.
- **Log:** right-click the icon and choose **Open the log**. It helps understand what happened.

---

## Como funciona / How it works

O TrocaJunto fala **HID++ 2.0**, o protocolo da Logitech, direto pelo Bluetooth (coleção HID `FF43/0202`), sem drivers e sem pacotes de terceiros.

TrocaJunto speaks Logitech's **HID++ 2.0** protocol directly over Bluetooth (HID collection `FF43/0202`), with no drivers and no third-party packages.

1. Quando o teclado conecta, o programa **desvia** temporariamente as teclas Easy-Switch (CIDs `0x00D1`–`0x00D3`, recurso `REPROG_CONTROLS_V4` `0x1B04`). Desviadas, elas avisam o programa em vez de trocar o teclado sozinhas. / When the keyboard connects, the Easy-Switch keys are temporarily **diverted**, so they notify the app instead of switching the keyboard by themselves.
2. Ao apertar uma delas, o programa manda primeiro o mouse e depois o teclado para o canal escolhido (`CHANGE_HOST` `0x1814`). / On key press, it sends the mouse, then the keyboard, to the chosen channel.
3. O desvio é **temporário**: o próprio teclado o esquece ao trocar de computador ou ao ser desligado. Por isso as teclas funcionam normalmente num computador sem o TrocaJunto. / The diversion is **temporary**: the keyboard forgets it when it changes host or is power-cycled, so the keys work normally on computers without TrocaJunto.

### Compilar / Build from source

Precisa do [.NET 10 SDK](https://dotnet.microsoft.com/download). / Requires the .NET 10 SDK.

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publicar
```

Cada tag `v*` enviada ao GitHub gera a versão para download automaticamente (`.github/workflows/release.yml`). / Every `v*` tag pushed to GitHub builds a downloadable release automatically.

---

## Licença / License

[MIT](LICENSE). Projeto independente, **sem ligação com a Logitech**. Logitech, Logi, MX Keys, MX Master, Easy-Switch, Unifying e Bolt são marcas da Logitech. / Independent project, **not affiliated with Logitech**. Logitech, Logi, MX Keys, MX Master, Easy-Switch, Unifying and Bolt are trademarks of Logitech.
