# Implicit UI — Guia de Projeto

> **Documento de contexto inicial para o Claude Code.**
> Documento vivo. Se algo aqui conflitar com uma instrução direta no chat, a instrução no chat vence — e o documento deve ser atualizado depois.
> Última atualização: 14/09/2026 (decisões de nomes, asmdef, licença do shader e contexto de pipelines) · Estado: **nada implementado; Fase 0 liberada**

---

## 0. Como trabalhar neste projeto

### Antes de escrever código

1. **Não assumir.** Onde este documento marca algo como pendente ou a verificar, **parar e perguntar**, nunca escolher sozinho.
2. **Não inventar features.** O escopo está em §2. Se algo parecer uma boa adição, propor antes de implementar.
3. **Confirmar antes de trabalhos grandes.** Apresentar o plano da fase e esperar aprovação antes de implementar.

### Ao escrever código

4. **Nunca copiar código do `com.unity.ugui`.** Licença Unity Companion, incompatível com MIT. Toda geometria e toda lógica de UI é escrita do zero. Consultar o comportamento nativo para ter paridade é ok; copiar não é.
5. **Raciocínio antes do resultado.** Explicar o passo a passo separado do código final.
6. **Um asmdef de runtime e um de editor**, com as features organizadas em pastas. Runtime e Editor sempre em asmdefs distintos. *(Decidido em 14/09/2026 — substitui "uma feature por asmdef".)*
7. **Zero dependências externas.** O `com.unity.ugui` é da própria Unity e **é declarado** no `package.json` como `"com.unity.ugui": "1.0.0"` (resolve para o ugui embutido de cada editor). TMP entra por version define (§3.3), nunca como dependência declarada. *(Decidido em 14/09/2026.)*
8. **Nada de API interna da Unity por reflection** sem aprovação explícita.
9. **API pública documentada** com XML docs, em inglês.
10. **Zero warnings** de compilação.
11. **Desenvolvimento na Unity 6.6.0f1**, mas o código precisa compilar no piso definido em §11. Desenvolver na versão mais nova facilita usar API nova sem perceber — quando em dúvida sobre a disponibilidade de uma API, checar §11 antes de usar.

### Definition of Done (por feature)

- [ ] Testes EditMode cobrindo os casos de borda da seção da feature
- [ ] Testes PlayMode quando houver comportamento em runtime
- [ ] CI verde em todas as versões da matriz
- [ ] Compila e roda com IL2CPP + stripping alto *(verificação manual — build de player fora do CI, decidido em 14/09/2026)*
- [ ] Cena de sample demonstrando a feature
- [ ] Seção no README
- [ ] XML docs na API pública
- [ ] Passa no teste do princípio implícito (§1)

---

## 1. Visão geral

**Implicit UI** — package Unity gratuito e open source (MIT) com melhorias para componentes de UI (uGUI). Segundo package da linha, depois do **ImplicitSave**, com a mesma proposta editorial: resolver dores reais com o mínimo de configuração possível.

### Princípio de design (não negociável)

**Quanto menos o usuário precisar fazer, melhor.**

- Adicionar o componente deve bastar. Sem material para arrastar, sem prefab de setup, sem ScriptableObject central, sem registro manual.
- Se existe um estado do engine que já responde a pergunta (ex.: `Selectable.interactable`), o componente reage a ele sozinho.
- Configuração explícita existe como *override*, nunca como requisito.
- Se uma feature não consegue ser implícita, precisa justificar por quê — e a parte explícita deve ser a menor possível.

### Duas categorias de feature

| Pilar | O que é | Regra |
|---|---|---|
| **Componentes de runtime** | Sliced+Filled, Grayscale, Hitbox, TextSizeGroup | Precisam ser implícitos: adicionar e funcionar |
| **Ferramentas de editor** | Font Changer, e o backlog de diagnóstico | Explícitas por natureza. O implícito aqui vira **não exigir setup prévio** — nenhum asset de config, nenhuma marcação manual |

### Régua de qualidade (herdada do ImplicitSave)

Testes EditMode + PlayMode, CI GameCI em Docker na matriz de §11, compatível com IL2CPP stripping alto, zero dependências externas, suporte via GitHub Issues com templates, semver, tag publica no OpenUPM, Asset Store manual.

---

## 2. Escopo

### 2.1 v1.0

| # | Feature | Tipo | Seção |
|---|---|---|---|
| 1 | Sliced + Filled — Horizontal/Vertical **e** Radial, só pelo componente `ImplicitFill` (sem wrapper de `Image`) | Runtime | §4 |
| 2 | Grayscale / desaturação — `Image` e `RawImage` (TMP fica para 1.1) | Runtime | §5 |
| 3 | Hitbox maior que o visual — padding **e** modo dedo mínimo (dp) | Runtime | §6.1 |
| 4 | Tamanho de fonte sincronizado | Runtime | §6.2 |
| 5 | Font Changer — modo cena **e** modo diretório | Editor | §6.3 |

**Render pipelines na v1.0:** Built-in, URP **e HDRP**. Ver §11.4 — HDRP entra como objetivo, com ponto de decisão explícito caso os testes mostrem que sai caro demais.

### 2.2 Pós-v1.0

| Item | Versão alvo |
|---|---|
| Grayscale em TMP (exige variante do shader SDF — §5.4) | 1.1 |
| Font Changer: mapeamento de múltiplos materiais por preset (§6.3) | 1.1 |
| Debug de canvas rebuild / batch (§7.1) | Backlog |
| Analyzer de âncoras / resolução (§7.2) | Backlog |
| Layout Group mais leve (§7.3) | Backlog |

### 2.3 Descartado — e por quê

Antes de propor feature nova, checar esta tabela.

| Dor | Motivo |
|---|---|
| Foco/navegação de gamepad | Nichado demais |
| Blur | UIEffect já cobre; caro de resolver bem |
| Cantos arredondados | nobi.roundedcorners, gilzoide, packages SDF |
| Safe area / notch | Nativo no uGUI 2.6 |
| Soft mask / unmask | mob-sakai (SoftMaskForUGUI, UnmaskForUGUI) |
| Outline / Shadow melhores | Parcialmente coberto pelo UIEffect |
| ScrollRect com pooling | Muitas opções existentes |
| ScrollRect aninhado | Scripts soltos, sem espaço claro |
| Transição de Selectable sem Animator | Território de DOTween e afins |
| Tooltips, drag & drop, gestos | Específico demais de cada jogo; fere o princípio implícito |
| `CanvasGroup.alpha` sujando cor dos filhos | Exigiria contornar o pipeline de vértices inteiro |

---

## 3. Estrutura e convenções

### 3.1 Identidade

- **Nome:** Implicit UI
- **Repositório:** novo, separado do ImplicitSave
- **Package id:** `com.bionics.implicitui`
- **Display name:** `Implicit UI`
- **Namespace:** `ImplicitUI` (runtime) e `ImplicitUI.Editor`, sem prefixo `Bionics.` — mesmo padrão do ImplicitSave. Testes: `ImplicitUI.Tests` (PlayMode) e `ImplicitUI.Tests.EditorTests` (EditMode, para não sombrear `UnityEditor.Editor`)
- **Publisher:** Bionics
- **Licença:** MIT
- **Título da Asset Store:** pode ser mais descritivo que o nome do package, no mesmo estilo do ImplicitSave — *a definir na publicação*

### 3.2 Layout do repositório

```
/
├── Packages/
│   └── com.bionics.implicitui/
│       ├── package.json
│       ├── README.md
│       ├── LICENSE.md
│       ├── CHANGELOG.md
│       ├── Runtime/
│       │   ├── <Feature>/
│       │   └── ImplicitUI.Runtime.asmdef
│       ├── Editor/
│       │   ├── <Feature>/
│       │   └── ImplicitUI.Editor.asmdef
│       ├── Tests/
│       │   ├── Runtime/            ImplicitUI.Tests.Runtime.asmdef
│       │   └── Editor/             ImplicitUI.Tests.Editor.asmdef
│       └── Samples~/
│           └── <Feature>Demo/
├── .ci/
│   ├── compat-host/
│   └── make-assetstore-copy.py
└── .github/
    ├── workflows/
    └── ISSUE_TEMPLATE/
```

Os scripts de CI e o `make-assetstore-copy.py` devem ser portados lendo direto do repositório do ImplicitSave na Fase 0, não de uma cópia neste documento.

### 3.3 Convenções

- **Menu de componentes:** `Add Component > UI > Implicit UI > <Componente>`, via `[AddComponentMenu]`.
- **Janelas de editor:** `Window > Bionics > Implicit UI > <Ferramenta>`.
- **Shaders:** nome `Bionics/ImplicitUI/<Nome>`. Nunca `Custom/`.
- **Sem `Resources/`.**
- **Idioma:** inglês em README, CHANGELOG, XML docs, labels e mensagens do editor.
- **TMP por version define:** no asmdef, `versionDefines` apontando para o package da TMP, gerando um símbolo próprio (ex.: `IMPLICITUI_TMP`). Todo código TMP fica sob `#if`. ⚠️ Ver §11.3 — a TMP mudou de package host na Unity 6, e a expressão do version define precisa ser verificada empiricamente nas duas situações.
- **Nomes de componente — prefixo `Implicit`** *(decidido em 14/09/2026)*, com o sufixo do tipo base quando o componente herda de um tipo da Unity (ex.: `...Image`): `ImplicitFill`, `ImplicitGrayscale`, `ImplicitHitbox`, `ImplicitTextSizeGroup`. **Sem wrapper de `Image`** *(decidido em 14/09/2026)*: o Sliced+Filled existe só como `ImplicitFill` — um caminho público para cada coisa.

---

## 4. Feature 1 — Sliced + Filled (principal)

### 4.1 Problema

No uGUI, `Image.Type` é mutuamente exclusivo: ou **Sliced** (9-slice, escala sem deformar bordas) ou **Filled** (`fillAmount` 0–1). Nunca as duas.

Consequência: barras de vida/progresso com moldura ou cantos arredondados não podem ser redimensionadas. A gambiarra universal é um **Slider com interação desativada** fingindo ser fill — hierarquia extra, componente interativo desnecessário, e um `value` que não é `fillAmount`.

### 4.2 Restrição técnica central

`UnityEngine.UI.Image.Type` é um **enum fechado**. Não dá para adicionar `SlicedAndFilled` a ele.

### 4.3 Arquitetura decidida

**Motor: `IMeshModifier`.** O `Image` continua nativo com `type = Sliced` (ou Tiled, ou Simple). O componente recebe a mesh **já gerada pela Unity** e recorta os triângulos no `fillAmount`, interpolando UV e cor.

Herda de graça 9-slice, tiling, atlas, `preserveAspect`, `fillCenter`, `pixelsPerUnitMultiplier` — e evita o problema de licença de §0.4, porque não reimplementa nada.

**Embalagem: só o componente `ImplicitFill`** *(decidido em 14/09/2026)*. O wrapper `SlicedFilledImage : Image` com `ImageEditor` custom foi **descartado**: seriam dois caminhos públicos para a mesma coisa, trocar o `Image` de prefabs existentes fere o princípio implícito (§1) e quebra referências serializadas, e manter uma subclasse de `Image` + editor custom nas 4 linhas da matriz custa caro. O que se perde — o "SlicedAndFilled" no dropdown de `type` — é compensado por README e sample.

**Campos de fill nativos.** O `ImplicitFill` não tem `fillAmount` próprio: lê `fillAmount`, `fillMethod`, `fillOrigin` e `fillClockwise` do próprio `Image`. Esses campos existem no `Image` qualquer que seja o `type`, e os setters só chamam `SetVerticesDirty()` (verificado no ugui da 6000.6). Assim código existente (`image.fillAmount = hp`) continua valendo e não há dois `fillAmount` no mesmo objeto. O inspector do `ImplicitFill` expõe esses campos, já que o do `Image` os esconde quando `type` não é Filled.

### 4.4 Algoritmo

Recorte de polígono contra semiplano (Sutherland–Hodgman), triângulo a triângulo:

1. Ler a mesh via `VertexHelper.GetUIVertexStream`.
2. Classificar os 3 vértices contra a reta de corte (dentro/fora).
3. Nas arestas que cruzam, calcular `t` e interpolar **todos os canais do `UIVertex`**: posição, normal, tangent, cor e UV0–UV3. *(Fase 1, 15/09/2026, confirmado pelo autor: ampliado de "posição, UV0, UV1 e cor". A mesh nativa do `Image` só preenche posição, cor e UV0 — os outros canais importam quando um mesh modifier **anterior** ao `ImplicitFill` grava neles, como `PositionAsUV1`, efeitos de terceiros e a intensidade por vértice do `ImplicitGrayscale` da Fase 4. Custo: só os vértices criados no corte.)* A referência do corte é o **bounds da mesh gerada**, não o `RectTransform` — bate com o Filled nativo e acompanha `preserveAspect`, padding e bordas encolhidas.
4. Re-triangular o polígono resultante (0, 3 ou 4 vértices → 0, 1 ou 2 triângulos).
5. Reescrever o `VertexHelper`.

Horizontal/Vertical = **um** semiplano. Radial = sequência de clips angulares por quadrante.

⚠️ **Canais da mesh × interpoladores do shader** *(esclarecido em 15/09/2026)*: o `TEXCOORD2` do `RectMask2D` moderno é um **interpolador do shader** (saída do vertex para o fragment), calculado a partir da posição do vértice — **não é um canal da mesh**. Verificado no `TMP_SDF-Mobile.shader` da TMP instalada na 6.6: a mesh entra por `POSITION`, `NORMAL`, `COLOR`, `TEXCOORD0` e `TEXCOORD1`; `mask : TEXCOORD2` existe só no struct de saída. Para o recorte, interpolar os canais da mesh nunca interfere com a máscara. Ver §5.3.

### 4.5 Requisitos funcionais

- `fillMethod`: Horizontal, Vertical, Radial 90/180/360, com `clockwise`. *(Fase 1: Horizontal e Vertical. Com método radial o `ImplicitFill` não recorta e o inspector avisa, até a Fase 2.)*
- `fillOrigin`: paridade com o `Image.Type.Filled` nativo.
- `fillAmount`: 0–1.
- Mudar o fill chama **apenas `SetVerticesDirty()`**, nunca `SetLayoutDirty()`. Como os campos são os nativos do `Image` (§4.3), isso vem do próprio setter da Unity; o `ImplicitFill` não pode introduzir nenhum caminho que suje layout. É o que torna a animação barata e é o diferencial sobre a gambiarra do Slider.
- Com `Image.type = Filled` o `Image` já recorta sozinho: o `ImplicitFill` **não recorta de novo** (seria fill aplicado duas vezes) e avisa no inspector.
- Funciona com `Mask` (stencil) e `RectMask2D`.
- Funciona com sprite em atlas e com sprite sem border (degrada para Simple + fill).
- `fillAmount = 0` não gera geometria (nada de quad degenerado).

### 4.6 Casos de teste obrigatórios

- `fillAmount` exatamente sobre o limite da borda esquerda do 9-slice.
- `fillAmount` dentro da região central esticada.
- `fillAmount` dentro da borda direita.
- `fillAmount` = 0 e = 1 (1 deve bater vértice a vértice com o Image nativo).
- Rect menor que a soma das bordas (Unity encolhe as bordas — o recorte acompanha).
- Dentro de `RectMask2D` e de `Mask`.
- Sprite em atlas vs sprite solto. *(Fase 1: nos testes automáticos, "atlas" = sprite sobre um pedaço de uma textura maior, com UVs fora de 0..1 — é o que importa para a interpolação. Sprites nativos `UI/Skin/UISprite.psd` (9-slice) e `UI/Skin/Knob.psd` (círculo). Atlas real só no teste manual.)*
- Com `preserveAspect` ligado.
- Cada `fillMethod` radial em cada `fillOrigin`.
- Verificação de que mudar `fillAmount` **não** dispara layout rebuild — em todas as linhas da matriz, porque os setters usados são os do `Image` nativo (ugui 1.0 na 2021.3/2022.3, ugui 2.0 na Unity 6).
- `Image.type = Filled` com `ImplicitFill` presente: nenhum recorte duplicado.
- Mudar `fillMethod` zera `fillOrigin` (comportamento do setter nativo) — o `ImplicitFill` acompanha sem estado próprio desatualizado.

### 4.7 Diferencial declarado

Contra o gist do yasirkula (o que a maioria usa hoje): Tiled suportado, radial suportado, masking testado, sem layout rebuild, testes automatizados, CI multi-versão, suporte por issues, instalável por OpenUPM/UPM.

---

## 5. Feature 2 — Grayscale / desaturação

### 5.1 Posicionamento

O produto é o **componente**, não o shader. "Um material grayscale" não compete com o UIEffect. O que ninguém cobre é o lado implícito: o caso real é **botão desabilitado ficar cinza**, e hoje isso exige material, script e cuidado com batching.

### 5.2 Design do componente

`ImplicitGrayscale`:

- **Não expõe material.** Resolve e compartilha internamente. O usuário nunca arrasta nada.
- **Auto-hook em `Selectable`**: se houver um `Selectable` no mesmo objeto ou acima, reage a `interactable` sozinho. Botão desabilitado fica cinza sem uma linha de código. *(Padrão, e principal argumento de venda.)*
- **Propaga para os filhos** por padrão: um componente no root do painel deixa o painel inteiro cinza.
- **Intensidade 0–1** por **canal de vértice**, não por instância de material — assim elementos com intensidades diferentes continuam no mesmo batch.
- Tint opcional sobre o cinza (sépia, azulado etc.), também por canal de vértice quando possível.
- v1.0: `Image` e `RawImage`. TMP em 1.1 (§5.4).
- **Aninhado — suportado** *(decidido em 14/09/2026)*. Ex.: painel cinza com um botão filho que tem o próprio `Selectable`. Regra proposta, a confirmar na Fase 4: intensidade efetiva = **máximo** entre a do próprio componente e a dos ancestrais (um filho nunca fica "menos cinza" que o painel; um botão desabilitado dentro de um painel normal fica cinza).

#### Enter Play Mode sem domain reload

Com *Enter Play Mode Options* ligado e *Reload Domain* desligado (padrão do template URP, e ligado no projeto de desenvolvimento), **campos `static` sobrevivem entre sessões de Play**. O grayscale é o caso de risco porque compartilha material e estado internamente.

Não dá (nem precisa) forçar um domain reload. O que resolve é o package **zerar os próprios estáticos**, que é o caminho recomendado pela Unity:
- Material compartilhado com getter preguiçoso e checagem de nulo da Unity (`if (!s_Material)`), com `HideFlags.HideAndDontSave` — se o objeto foi destruído, é recriado.
- Qualquer registro/cache estático (lista de componentes ativos, contadores) zerado num método `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`.
- Evitar `static event` sem desinscrição.

Custo: poucas linhas. Verificação: manual, rodando Play duas vezes seguidas no projeto de desenvolvimento, que já tem a opção ligada. A mesma regra vale para qualquer outra feature que venha a ter estado estático.

### 5.3 Shader

**Partir do `UI-Default.shader` oficial** e adicionar apenas a luminância e o lerp no fim do `frag`. Herda pragmas, clipping, instancing e estados corretos.

**Licença — verificado em 14/09/2026:** o `UI/Default` não vem do `com.unity.ugui` (Unity Companion License); fica em `unity_builtin_extra`, e o fonte dos built-in shaders é distribuído pela Unity sob **MIT** (cabeçalho: *"Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license"*). Derivar é permitido, desde que:
- o fonte venha do arquivo de built-in shaders **da versão certa** (a da matriz que define o piso), não de um mirror;
- o cabeçalho de copyright seja mantido no `.shader`;
- o aviso MIT da Unity entre num `Third Party Notices.md` do package (a Asset Store também pede isso na página do asset).

```hlsl
float luminance = dot(col.rgb, float3(0.299, 0.587, 0.114));
fixed3 neutralGray = luminance.xxx;
fixed3 tintedGray  = neutralGray * _TintColor.rgb;
fixed3 finalGray   = lerp(neutralGray, tintedGray, _TintIntensity);
col.rgb = lerp(col.rgb, finalGray, _GrayAmount);
```

⚠️ **Colisão de canal de vértice — verificar antes de fixar o design.** O `RectMask2D` moderno passa os dados de máscara por `TEXCOORD2`. Se a intensidade do grayscale for por canal de vértice, precisa ocupar um canal livre e coexistir com isso. Esse é exatamente o problema que o UIEffect enfrentou ao tentar usar `TEXCOORD2` para outros fins. **Confirmar empiricamente qual canal está livre** antes de escrever o shader.
*(Esclarecido em 15/09/2026, ver §4.4: são duas coisas diferentes. O `TEXCOORD2` da máscara é um interpolador do shader, não um canal da mesh. A intensidade pode ir num canal da mesh (UV1–UV3 — conferir quais a TMP lê, ela usa `TEXCOORD1`), e no shader do grayscale ela precisa sair por um slot de interpolador diferente de `TEXCOORD2`. Falta conferir o `UI-Default` da versão certa: o mirror público é antigo e não tem a softness.)*

**Armadilhas já identificadas** (um shader de grayscale de projeto anterior tinha todas — são os erros típicos de shader de UI escrito à mão):

1. **Pragmas ausentes** `#pragma multi_compile_local _ UNITY_UI_CLIP_RECT` e `_ UNITY_UI_ALPHACLIP`. Sem eles a keyword nunca é definida, o bloco de clipping vira código morto e **o shader não respeita `RectMask2D`** (com `Mask`/stencil funciona).
2. **Erro de sintaxe escondido no código morto** — `saturate(` sem fechar. Só não quebrava o build porque nunca compilava.
3. **Fórmula de clipping errada:** comparava com o canto do `_ClipRect` em vez do centro, e multiplicava por `worldPosition.w` (que vale 1) em vez de usar a softness. `_UIMaskSoftnessX/Y` declarados e nunca usados.
4. **`clip(col.a - 0.001)` incondicional** — o `UI/Default` só faz isso sob `UNITY_UI_ALPHACLIP`. Um `discard` sempre ativo marca o fragment shader como "pode descartar" e desliga otimizações de tile em mobile (PowerVR / early-Z).
5. **Sem instancing/stereo** (`UNITY_VERTEX_INPUT_INSTANCE_ID`, `UNITY_VERTEX_OUTPUT_STEREO`): quebra Single Pass Instanced em VR.

Ponto em aberto: precisão `fixed` vs `half` para a luminância em mobile (risco de banding). Decidido na implementação.

### 5.4 Por que TMP fica para 1.1

A TMP não renderiza com shader de UI comum — usa SDF (`TextMeshPro/Distance Field`), com atlas, dilate, outline e underlay próprios. Trocar o material de um `TMP_Text` por um shader de UI comum **quebra a renderização do texto**. Suportar TMP exige uma variante separada derivada do `TMP_SDF.shader`, o que praticamente dobra o trabalho da feature. Fica para 1.1.

---

## 6. Features 3 a 5

### 6.1 Hitbox maior que o visual

**Problema:** ícone de 24×24 é impossível de acertar no dedo. Gambiarras: Image transparente maior atrás, ou inflar o RectTransform e compensar com um filho.

**Solução:** componente implementando `ICanvasRaycastFilter` que expande a área aceita por padding (top/bottom/left/right, ou valor único). Raycast aceita, visual não muda, hierarquia não muda.

**Na v1.0, os dois modos:**
- Padding em unidades de UI.
- **Modo dedo mínimo (dp)**: garantir um alvo mínimo, **padrão 48dp** *(decidido em 14/09/2026)*, calculado a partir do `Canvas.scaleFactor` e do DPI da tela. Cuidado: `Screen.dpi` retorna 0 em algumas plataformas — precisa de fallback definido e testado *(valor do fallback: aberto, Fase 5)*.

**Bônus no mesmo componente — envelopar `alphaHitTestMinimumThreshold`:** hoje **lança exceção em runtime** se a textura do sprite não estiver com Read/Write enabled, com mensagem que não explica a causa. O componente expõe a opção de forma amigável, valida no editor antes do erro, e oferece o fix de import settings com um clique.

### 6.2 Tamanho de fonte sincronizado entre irmãos

**Problema:** TMP tem Auto Size, mas cada texto calcula sozinho. Um menu com "OK", "Configurações" e "Voltar ao menu principal" fica com três tamanhos diferentes. É por isso que quase todo mundo desliga auto size e ajusta na mão.

**Solução:** `ImplicitTextSizeGroup` no objeto pai.
- Calcula o auto size resolvido de cada filho e aplica o **menor** a todos.
- Recalcula quando o texto, o rect ou a lista de filhos muda.
- TMP (sob version define) e `Text` legacy.

**Políticas decididas:**
- Filhos **desativados**: ignorar.
- Filhos com **auto size desligado**: ignorar por padrão, com bool para forçar.

**Cuidado:** não pode entrar em loop de rebuild (mudar fontSize dispara layout, que dispara recálculo). Precisa de dirty flag e de aplicar fora do ciclo de rebuild.

### 6.3 Font Changer (ferramenta de editor)

**Problema:** trocar a fonte de um projeto inteiro é manual e propenso a esquecer objetos. Acontece em rebranding, troca de licença de fonte, suporte a novo idioma.

**Escopo funcional (tudo na v1.0):**
- Campos **From** e **To** (fonte). From vazio = trocar *todas* as fontes encontradas.
- **Campo de material, opcional:** se preenchido, aplica esse material nos textos alterados; se vazio, usa o material default da fonte de destino.
- Dois modos de alvo:
  - **Cena** — varre os objetos de **todas as cenas carregadas**, incluindo os **desativados**. Não abre nem toca em cenas fechadas.
  - **Diretório** — varre **prefabs** dentro de uma pasta; bool para incluir subpastas. Cenas em disco não entram neste modo, por design.
- **Log** do que foi alterado, com caminho do asset, cena de origem e caminho do objeto na hierarquia (pai > filho > neto), **clicável** para dar ping.

**Requisitos e riscos — é a feature mais perigosa do package:**

1. **Operação destrutiva em massa e parcialmente não desfazível.**
   - Em cena: `Undo.RecordObject` cobre.
   - Em prefab asset: **Undo não cobre**. Caminho correto: `PrefabUtility.LoadPrefabContents` → alterar → `SaveAsPrefabAsset` → `UnloadPrefabContents`.
   - **Dry-run obrigatório.** Primeiro varre e mostra a lista do que *seria* alterado, com checkbox por item; só aplica depois de confirmar. Nunca aplicar direto.
   - Aviso explícito recomendando controle de versão.

2. **Overrides de prefab — o erro que quase toda implementação comete.**
   Se o objeto na cena é instância de prefab e a fonte vem herdada, alterar a instância cria um **override** em cada instância, espalhando sujeira pelo projeto.
   - Valor **herdado** → **pular** por padrão, deixando o modo diretório resolver no prefab de origem.
   - Valor que **já era override** → alterar.
   - Tratar prefab variants e prefabs aninhados.

3. **`Text` e TMP são tipos diferentes.** `Text.font` é `Font`; `TMP_Text.font` é `TMP_FontAsset`. Sem conversão direta — dois modos separados na ferramenta.

4. **Validação obrigatória do material escolhido.**
   Um material preset da TMP está amarrado ao **atlas de uma fonte específica** (o `_MainTex` do material aponta para o atlas daquela fonte). Aplicar um preset da fonte A num texto que agora usa a fonte B renderiza lixo.
   - Antes de aplicar, validar que o material informado pertence à fonte de destino.
   - Se não pertencer, **bloquear** com mensagem clara no dry-run, não avisar e seguir.
   - Considerar também `fontMaterial` (instância) e listas de fallback.

5. **Presets múltiplos — requisito da v1.0.**
   Um projeto costuma ter vários materiais para a mesma fonte (No Outline, BigOutline, ThickerOutline, RedOutline…). Um único campo de material achata todos em um só, e o usuário pode não perceber que perdeu os presets.
   Dois requisitos, ambos na v1.0:
   - O dry-run **agrupa os objetos pelo material atual**, mostrando quantos usam cada um, antes de qualquer alteração.
   - Filtro opcional **"from material"**, combinável com o "from font", para o usuário rodar a ferramenta uma vez por preset e preservar a variedade.
   - Mapeamento automático material-antigo → material-novo continua fora da v1.0 (fica para 1.1).

6. **Métricas diferentes quebram layout.** Fontes diferentes têm altura de linha e largura distintas — após a troca, textos podem estourar ou sobrar. Fora do escopo corrigir, mas o log deve avisar.

7. **Varrer desativados — a API óbvia não serve.**
   `FindObjectsOfType<T>()` **ignora objetos desativados**, e painéis de UI vivem desativados. `FindObjectsByType<T>(FindObjectsInactive.Include, ...)` só existe da 2022.2 em diante, o que amarraria o piso (§11).
   **Caminho recomendado:** iterar as cenas carregadas via `SceneManager.GetSceneAt(i)` → `GetRootGameObjects()` → `GetComponentsInChildren<T>(true)`. Funciona em todas as versões e inclui desativados.
   **Prefab Mode aberto:** quando o usuário está com um prefab aberto em isolamento, a "cena aberta" é o prefab stage. Detectar via `PrefabStageUtility.GetCurrentPrefabStage()` e operar sobre o conteúdo dele. ⚠️ O namespace mudou entre versões — ver §11.2.

8. **Performance:** `AssetDatabase.FindAssets("t:Prefab", folders)` e envolver a aplicação em `AssetDatabase.StartAssetEditing` / `StopAssetEditing`.

---

## 7. Backlog — estudo antes de virar promessa

### 7.1 Debug de UI + canvas rebuild (par)

A ferramenta de diagnóstico é o que justifica e valida qualquer otimização de rebuild. Sem medir, não dá para provar ganho.

Janela de editor, no espírito do Save Editor do ImplicitSave:
- Quais canvases sujaram neste frame e quantas vezes.
- **Quem** sujou (componente/objeto) — a informação que o Profiler não dá.
- Batches por canvas.
- **Por que** o batch quebrou: material, textura, elemento intercalado na ordem de desenho.

**Sub-item: auto-limpeza de `raycastTarget`.** Vem ligado por padrão em todo `Graphic`, e cada um entra no loop do `GraphicRaycaster`. Analyzer que detecta Graphics sem componente interativo (e sem `ICanvasRaycastFilter` relevante) e oferece desligar em lote. Mora dentro da janela de debug, não como feature solta.

### 7.2 Analyzer de âncoras / resolução

Detecta elementos que vão quebrar em outras resoluções: ancorados ao centro com tamanho fixo, dependentes de aspect ratio específico, saindo da tela.

- Flag **`isMobile`**: quando ligada, testa contra um conjunto maior de telas, porque a variação de aspect ratio em mobile é muito maior.
- Precisa conviver com o **Device Simulator**, não competir.

**Risco:** a lista de devices do Simulator e os tamanhos do Game View são APIs internas; acessá-las exige reflection e quebra entre versões (proibido por §0.8 sem aprovação).
**Direção:** lista curada de resoluções própria (desktop e mobile), integrando com o Simulator apenas de forma leve — por exemplo avaliando o aspect ratio em que ele está no momento.

### 7.3 Layout Group mais leve

Custo dominante: cada rebuild faz `GetComponents<ILayoutElement>` em **todo** filho, e o rebuild é disparado por quase qualquer mudança.

Direção: cache dos `ILayoutElement` invalidado em `OnTransformChildrenChanged`; dirty flag granular por eixo.

Encaixe no princípio implícito: **drop-in replacement** — troca o componente e ganha performance sem mudar mais nada. **Exigência: benchmark antes de anunciar.**

---

## 8. Risco estratégico — UI Toolkit

A Unity posiciona o UI Toolkit como o futuro da UI, e este package aposta em uGUI.

**Avaliação:** o uGUI segue dominante em jogos — o UI Toolkit ainda tem lacunas em world-space, performance de runtime e workflow de artista. Risco de médio prazo, não imediato.

**Decisão:** seguir com uGUI. Versão para UI Toolkit não está definida; fica como questão aberta.

**Consequência do formato guarda-chuva:** o package envelhece inteiro de uma vez, em vez de permitir aposentar features isoladas. Trade-off aceito em troca de escopo claro na Asset Store.

---

## 9. Distribuição

- **OpenUPM** — automático por tag, já na v1.0.
- **Git URL** apontando para a subpasta do package.
- **Asset Store** — submeter já na v1.0, gratuito. A revisão demora, então submeter cedo é vantagem. Upload manual via Asset Store Publishing Tools.
- **Suporte:** GitHub Issues com templates de bug, dúvida e feature.

### Samples

Cena demo com comparação lado a lado: barra de vida com Sliced+Filled vs. a gambiarra do Slider desativado, ambas sendo redimensionadas ao vivo. É o print do README e da página da Asset Store.

---

## 10. Ordem de implementação

| Fase | Conteúdo | Depende de |
|---|---|---|
| 0 | Fundação do repo, CI, asmdefs, package.json, version define da TMP | — |
| 1 | Recortador de mesh + `ImplicitFill` (Horizontal/Vertical) | Fase 0 |
| 2 | Radial fill | Fase 1 |
| 3 | ~~Wrapper `SlicedFilledImage` + `ImageEditor` custom~~ — **removida em 14/09/2026** (§4.3); numeração mantida | — |
| 4 | `ImplicitGrayscale` (shader a partir do UI-Default + componente implícito; hosts de pipeline §11.4) | Fase 0 |
| 5 | `ImplicitHitbox` + modo dp + alpha hit test | Fase 0 |
| 6 | `ImplicitTextSizeGroup` | Fase 0 |
| 7 | Font Changer — dry-run, modo cena, modo diretório | Fase 0 |
| 8 | Samples, README, publicação v1.0.0 | Fases 1, 2 e 4–7 |
| 9 | Backlog: TMP grayscale, debug, analyzer, layout group | Pós-1.0 |

---

## 11. Compatibilidade de versão — levantamento preliminar

> **Status: preliminar.** O piso de versão **não está fechado** e pode mudar durante a implementação. Cada item abaixo marcado com ⚠️ precisa ser **verificado no editor**, não assumido. Ao encontrar divergência, atualizar esta seção.

**Desenvolvimento:** Unity 6.6.0f1.
**Matriz de CI (mesma do ImplicitSave, confirmada em 14/09/2026):** 2021.3.45f2, 2022.3.62f3, 6000.0.67f1, 6000.6.0f1 — as duas primeiras são os últimos builds que abrem com licença Personal. Sujeita a subir se algum item abaixo forçar.

**CI — decidido em 14/09/2026:**
- Repositório público; secrets `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` criados pelo autor. A licença é da conta Unity (não do projeto nem da máquina); os secrets são por repositório.
- Projeto host com **TMP instalada em todas as linhas** (`com.unity.textmeshpro` na 2021.3/2022.3; na Unity 6 ela já vem no `com.unity.ugui`), mais **um job que só compila sem TMP**, para pegar código TMP fora do `#if`.
- IL2CPP + stripping alto verificado **manualmente**, não no CI.

### 11.1 APIs seguras em todas as versões alvo

`VertexHelper` / `IMeshModifier` / `BaseMeshEffect`, `ICanvasRaycastFilter`, `Sprite.border` e `GetInnerUV`, `Image.pixelsPerUnitMultiplier`, `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` / `UnloadPrefabContents`, `AssetDatabase.StartAssetEditing` / `StopAssetEditing`, `Image.alphaHitTestMinimumThreshold`, `SceneManager.GetSceneAt` + `GetRootGameObjects`.

### 11.2 APIs que exigem `#if` por versão

| API | Situação | Ação |
|---|---|---|
| `PrefabStageUtility` | Saiu de `UnityEditor.Experimental.SceneManagement` para `UnityEditor.SceneManagement` na 2021.2 | `#if UNITY_2021_2_OR_NEWER` com os dois usings |
| `FindObjectsByType(FindObjectsInactive.Include, …)` | Só 2022.2+ | **Não usar.** Já resolvido pela varredura por roots (§6.3.7) |

### 11.3 ⚠️ Itens a verificar empiricamente antes de fixar o piso

1. **Version define da TMP.** Na Unity 6 a TextMeshPro passou a fazer parte do `com.unity.ugui` em vez de ser o package `com.unity.textmeshpro` separado. A expressão do `versionDefines` precisa cobrir **as duas situações**, ou o símbolo não é definido numa delas e a feature some silenciosamente. Testar na 2021.3 e na 6.6.
   **Fase 0 (15/09/2026):** os 4 asmdefs definem `IMPLICITUI_TMP` por duas regras — `com.unity.ugui` ≥ 2.0.0 ou `com.unity.textmeshpro` ≥ 3.0.0 — e referenciam `Unity.TextMeshPro` por nome. `TextMeshProSupport.IsCompiledIn` (Runtime) e `EditorTextMeshProSupport.IsCompiledIn` (Editor) expõem o resultado, e testes EditMode/PlayMode comparam com a presença real do tipo `TMPro.TMP_Text`. Verificado na 6.6 (TMP presente, as duas constantes `true`). **CI verde em 15/09/2026 nas 5 execuções** (2021.3 e 2022.3 com `com.unity.textmeshpro` 3.0.9, 6.0 e 6.6 com a TMP embutida no ugui, e 2022.3 sem TMP), 6/6 testes em cada, zero `warning CS`. O log do Unity do job sem TMP confirma que só o `com.unity.ugui@1.0.0` estava registrado. **Item resolvido.**
2. **`RectMask2D` e softness.** A softness e as propriedades `_UIMaskSoftnessX/Y` foram adicionadas depois do lançamento inicial do `RectMask2D`, e a TMP levou ainda mais tempo para respeitá-las. Se o shader de grayscale copiar o `UI-Default` da versão certa, isso se resolve sozinho — mas **confirmar em qual das versões da matriz o `UI-Default` já tem a softness** antes de assumir.
3. **Canal de vértice livre.** O `RectMask2D` moderno usa `TEXCOORD2` para dados de máscara. Confirmar qual canal está realmente livre para a intensidade do grayscale, em cada versão da matriz.
   *(15/09/2026: esse `TEXCOORD2` é interpolador do shader, não canal da mesh — ver §4.4 e §5.3. A pergunta passa a ser qual canal da mesh a TMP e o `UI-Default` leem em cada versão.)*
4. **`Screen.dpi` retorna 0** em algumas plataformas. Definir e testar o fallback do modo dp do `ImplicitHitbox`.

### 11.4 Render pipelines

**Objetivo da v1.0: Built-in, URP e HDRP.**

**Contexto da Unity — verificado em 14/09/2026** ([Render Pipelines strategy for 2026](https://unity.com/topics/render-pipelines-strategy-for-2026)):
- **HDRP em modo manutenção:** nenhuma feature nova planejada, só estabilidade e regressões; suporte até pelo menos o fim de 2028.
- **Built-in marcado como deprecated na 6.5**, disponível até a 6.7 LTS; remoção ainda não decidida.
- URP é o foco da Unity. Isso pesa no ponto de decisão de HDRP abaixo.

**Ambiente de teste — decidido em 14/09/2026: projetos host separados**, um por pipeline (não existe um pipeline que teste os outros; cada um renderiza a UI de Camera/World space do seu jeito). O projeto de desenvolvimento continua só em URP. **Criar os hosts só quando precisar** — na prática, na Fase 4 (grayscale), a única feature dependente de pipeline.

Quatro das cinco features da v1.0 (Sliced+Filled, Hitbox, TextSizeGroup, Font Changer) são **agnósticas de render pipeline** — mexem em mesh de UI, raycast, layout e assets, não em shading. **Só o grayscale depende de pipeline**, então o custo de HDRP está inteiramente concentrado nessa feature.

**Hipótese de trabalho a validar:**
- **Screen Space Overlay** deve funcionar em HDRP sem mudança, porque o overlay não passa pelo SRP.
- **Screen Space Camera** e **World Space** são o risco: a UI entra no render da câmera, e um shader escrito para o pipeline built-in pode não se comportar.

**Plano de teste (fase 4, junto com o grayscale):** cena de verificação com os três modos de canvas (Overlay, Camera, World), rodada manualmente nos três pipelines na versão de desenvolvimento.

**Ponto de decisão — parar e perguntar.** Se o teste mostrar que HDRP exige uma variante separada do shader (passes próprios de HDRP, Shader Graph etc.), trazer o resultado para discussão antes de implementar. As opções na mesa serão:
- (a) escrever e manter a variante HDRP;
- (b) declarar suporte só a Overlay em HDRP, documentado;
- (c) desistir de HDRP e voltar a Built-in + URP.

**CI:** a matriz de CI cobre **versões de Unity**, não pipelines — montar projeto host com HDRP multiplicaria o custo do CI por causa de uma feature. A verificação de pipeline é **manual**, com a cena de teste acima, repetida a cada mudança no shader. Registrar o resultado no README.

## 12. Decisões pendentes

1. Confirmar os nomes finais dos componentes (§3.3) na Fase 0.
2. Título da Asset Store (§3.1), a definir na publicação.
3. Piso de versão definitivo — depende de §11.3.
4. **HDRP**: decidir entre variante própria, Overlay-only ou desistir, depois do teste descrito em §11.4. Considerar o status de manutenção do HDRP.
### Decididas em 14/09/2026
- Ambiente de teste de pipelines: projetos host separados, criados só na Fase 4 (§11.4).
- Repositório público; matriz do ImplicitSave; TMP no host de CI + job sem TMP; IL2CPP manual (§0, §11).
- `com.unity.ugui` declarado como dependência (§0.7).
- Prefixo `Implicit` nos componentes, sufixo do tipo base quando herda (§3.3).
- Sliced+Filled só como `ImplicitFill`, usando os campos de fill nativos do `Image`; wrapper descartado e Fase 3 removida (§4.3, §10).
- README e sample de cada feature ficam para a Fase 8 (§10); durante as fases de feature só o CHANGELOG é atualizado. Resolve o conflito com a Definition of Done do §0, que pedia os dois por feature. Teste manual da fase usa cena de sandbox em `Assets/Sandbox/`.
- Grayscale aninhado suportado; estáticos zerados para Enter Play Mode sem domain reload (§5.2).
- Modo dp com padrão 48dp (§6.1). Pendente: fallback de `Screen.dpi == 0`.
- Nomes sem prefixo `Bionics.`, seguindo o ImplicitSave (§3.1).
- Um asmdef de runtime e um de editor (§0.6, §3.2).
- Display name `Implicit UI`.
- Este guia fica em português em `Docs/` por enquanto — é guia interno do projeto, não documentação para quem usa o package. Traduzir depois.
- Shader do grayscale derivado do `UI-Default` é compatível com MIT (§5.3).
