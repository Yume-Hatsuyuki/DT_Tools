/**
 * 界面文字总表。每行一个键，9 列依次为：
 * zh-CN, zh-TW, en, es, fr, ja, ko, pt-BR, ru
 * 游戏已有官方译法的词（设置、取消、确定、语言、平板电脑、线索、命题、学级裁判）直接沿用游戏文本。
 * 新增键时必须填满 9 列（测试会检查）；占位符用 {name}。
 */
const COLS = ['zh-CN', 'zh-TW', 'en', 'es', 'fr', 'ja', 'ko', 'pt-BR', 'ru'];

const ROWS = {
  // ---- 通用 ----
  'common.close': ['关闭', '關閉', 'Close', 'Cerrar', 'Fermer', '閉じる', '닫기', 'Fechar', 'Закрыть'],
  'common.closeHint': ['关闭（可从底部 Dock 再次打开）', '關閉（可從底部 Dock 再次開啟）', 'Close (reopen from the Dock)', 'Cerrar (se puede reabrir desde el Dock)', 'Fermer (rouvrir depuis le Dock)', '閉じる（下部 Dock から再度開けます）', '닫기 (하단 Dock에서 다시 열 수 있음)', 'Fechar (reabra pelo Dock)', 'Закрыть (можно открыть снова из Dock)'],
  'common.minimize': ['最小化', '最小化', 'Minimize', 'Minimizar', 'Réduire', '最小化', '최소화', 'Minimizar', 'Свернуть'],
  'common.maximize': ['最大化', '最大化', 'Maximize', 'Maximizar', 'Agrandir', '最大化', '최대화', 'Maximizar', 'Развернуть'],
  'common.restore': ['还原', '還原', 'Restore', 'Restaurar', 'Restaurer', '元に戻す', '복원', 'Restaurar', 'Восстановить'],
  'common.cancel': ['取消', '取消', 'Cancel', 'Cancelar', 'Annuler', 'キャンセル', '취소', 'Cancelar', 'Отмена'],
  'common.confirm': ['确定', '確定', 'Confirm', 'Confirmar', 'Confirmer', '決定', '결정', 'Confirmar', 'Подтвердить'],
  'common.refresh': ['刷新', '重新整理', 'Refresh', 'Actualizar', 'Actualiser', '更新', '새로고침', 'Atualizar', 'Обновить'],
  'common.loading': ['加载中…', '載入中…', 'Loading…', 'Cargando…', 'Chargement…', '読み込み中…', '불러오는 중…', 'Carregando…', 'Загрузка…'],
  'common.checking': ['检查中…', '檢查中…', 'Checking…', 'Comprobando…', 'Vérification…', '確認中…', '확인 중…', 'Verificando…', 'Проверка…'],
  'common.settings': ['设置', '設置', 'Settings', 'Opciones', 'Options', 'オプション', '옵션', 'Opções', 'Настройки'],
  'common.language': ['语言', '語言', 'Language', 'Idioma', 'Langue', '言語', '언어', 'Idioma', 'Язык'],
  'common.about': ['关于', '關於', 'About', 'Acerca de', 'À propos', '情報', '정보', 'Sobre', 'О программе'],
  'common.appMenu': ['应用菜单', '應用程式選單', 'App menu', 'Menú de aplicaciones', 'Menu des applications', 'アプリメニュー', '앱 메뉴', 'Menu de apps', 'Меню приложений'],

  // ---- 应用与小组件名称 ----
  'app.terminal': ['控制台', '控制台', 'Console', 'Consola', 'Console', 'コンソール', '콘솔', 'Console', 'Консоль'],
  'app.console': ['控制台（旧版）', '控制台（舊版）', 'Console (legacy)', 'Consola (antigua)', 'Console (ancienne)', 'コンソール（旧版）', '콘솔(구버전)', 'Console (legado)', 'Консоль (старая)'],
  'app.config': ['DT 配置', 'DT 設定', 'DT Config', 'Config DT', 'Config DT', 'DT 設定', 'DT 설정', 'Config DT', 'Настройки DT'],
  'app.automation': ['自动化', '自動化', 'Automation', 'Automatización', 'Automatisation', '自動化', '자동화', 'Automação', 'Автоматизация'],
  'app.dummy': ['假人管理', '假人管理', 'Dummy manager', 'Gestor de maniquíes', 'Gestion des mannequins', 'ダミー管理', '더미 관리', 'Gerenciar bonecos', 'Управление манекенами'],
  'app.mp3': ['随身MP3', '隨身MP3', 'Portable MP3', 'MP3 portátil', 'MP3 portable', 'ポータブルMP3', '휴대용 MP3', 'MP3 portátil', 'Портативный MP3'],
  'app.gamepad': ['手柄', '手把', 'Gamepad', 'Mando', 'Manette', 'ゲームパッド', '게임패드', 'Controle', 'Геймпад'],
  'app.tablet': ['平板电脑', '平板電腦', 'Tablet', 'Tableta', 'Tablette', 'タブレット', '태블릿', 'Tablet', 'Планшет'],
  'app.log': ['日志', '日誌', 'Log', 'Registro', 'Journal', 'ログ', '로그', 'Log', 'Журнал'],
  'widget.steam': ['Steam 当前（全球）玩家数统计', 'Steam 目前（全球）玩家數統計', 'Steam current (global) player count', 'Jugadores actuales en Steam (global)', 'Nombre de joueurs Steam (mondial)', 'Steam の現在の（全世界）プレイヤー数', 'Steam 현재(전 세계) 플레이어 수', 'Jogadores atuais no Steam (global)', 'Игроки Steam сейчас (весь мир)'],
  'widget.steamShort': ['Steam 在线', 'Steam 在線', 'Steam online', 'Steam en línea', 'Steam en ligne', 'Steam オンライン', 'Steam 접속', 'Steam online', 'Steam онлайн'],
  'widget.mcp': ['MCP 桥接（AI 接入）', 'MCP 橋接（AI 接入）', 'MCP bridge (AI access)', 'Puente MCP (acceso IA)', 'Pont MCP (accès IA)', 'MCP ブリッジ（AI 接続）', 'MCP 브리지(AI 연결)', 'Ponte MCP (acesso IA)', 'Мост MCP (доступ ИИ)'],
  'widget.news': ['游戏新闻（Steam 公告）', '遊戲新聞（Steam 公告）', 'Game news (Steam announcements)', 'Noticias del juego (anuncios de Steam)', 'Actualités du jeu (annonces Steam)', 'ゲームニュース（Steam のお知らせ）', '게임 뉴스(Steam 공지)', 'Notícias do jogo (anúncios Steam)', 'Новости игры (объявления Steam)'],

  // ---- 桌面 / Dock 菜单 ----
  'menu.openTerminal': ['打开终端', '開啟終端機', 'Open terminal', 'Abrir terminal', 'Ouvrir le terminal', 'ターミナルを開く', '터미널 열기', 'Abrir terminal', 'Открыть терминал'],
  'menu.wallpaper': ['更换壁纸…', '更換桌布…', 'Change wallpaper…', 'Cambiar fondo…', 'Changer le fond d’écran…', '壁紙を変更…', '배경화면 변경…', 'Alterar papel de parede…', 'Сменить обои…'],
  'menu.resetWallpaper': ['恢复默认壁纸', '還原預設桌布', 'Reset wallpaper', 'Restablecer fondo', 'Rétablir le fond par défaut', '壁紙を初期化', '기본 배경화면으로 복원', 'Restaurar papel de parede', 'Сбросить обои'],
  'menu.particles': ['粒子效果', '粒子效果', 'Particle effects', 'Efectos de partículas', 'Effets de particules', 'パーティクル効果', '파티클 효과', 'Efeitos de partículas', 'Эффекты частиц'],
  'menu.showDesktop': ['显示桌面', '顯示桌面', 'Show desktop', 'Mostrar escritorio', 'Afficher le bureau', 'デスクトップを表示', '바탕화면 보기', 'Mostrar área de trabalho', 'Показать рабочий стол'],
  'menu.minimizeAll': ['最小化所有进程', '最小化所有程序', 'Minimize all processes', 'Minimizar todos los procesos', 'Réduire tous les processus', 'すべてのプロセスを最小化', '모든 프로세스 최소화', 'Minimizar todos os processos', 'Свернуть все процессы'],
  'menu.closeAll': ['关闭所有进程', '關閉所有程序', 'Close all processes', 'Cerrar todos los procesos', 'Fermer tous les processus', 'すべてのプロセスを閉じる', '모든 프로세스 닫기', 'Fechar todos os processos', 'Закрыть все процессы'],
  'menu.rename': ['重命名…', '重新命名…', 'Rename…', 'Renombrar…', 'Renommer…', '名前を変更…', '이름 변경…', 'Renomear…', 'Переименовать…'],
  'menu.restoreName': ['恢复原名', '還原原名', 'Restore name', 'Restaurar nombre', 'Rétablir le nom', '元の名前に戻す', '원래 이름으로', 'Restaurar nome', 'Вернуть имя'],
  'menu.customIcon': ['自定义图标…', '自訂圖示…', 'Custom icon…', 'Icono personalizado…', 'Icône personnalisée…', 'カスタムアイコン…', '사용자 지정 아이콘…', 'Ícone personalizado…', 'Свой значок…'],
  'menu.restoreIcon': ['恢复默认图标', '還原預設圖示', 'Restore default icon', 'Restaurar icono', 'Rétablir l’icône', '既定のアイコンに戻す', '기본 아이콘으로', 'Restaurar ícone', 'Вернуть значок'],
  'menu.open': ['打开', '開啟', 'Open', 'Abrir', 'Ouvrir', '開く', '열기', 'Abrir', 'Открыть'],
  'menu.exitGame': ['退出游戏', '結束遊戲', 'Exit game', 'Salir del juego', 'Quitter le jeu', 'ゲームを終了', '게임 종료', 'Sair do jogo', 'Выйти из игры'],
  'menu.account': ['账户', '帳戶', 'Account', 'Cuenta', 'Compte', 'アカウント', '계정', 'Conta', 'Аккаунт'],
  'menu.changeUser': ['修改用户名…', '修改使用者名稱…', 'Change username…', 'Cambiar nombre de usuario…', 'Modifier le nom d’utilisateur…', 'ユーザー名を変更…', '사용자 이름 변경…', 'Alterar nome de usuário…', 'Сменить имя пользователя…'],
  'menu.changeAvatar': ['更换头像…', '更換頭像…', 'Change avatar…', 'Cambiar avatar…', 'Changer l’avatar…', 'アバターを変更…', '아바타 변경…', 'Alterar avatar…', 'Сменить аватар…'],
  'menu.resetAvatar': ['恢复默认头像', '還原預設頭像', 'Reset avatar', 'Restablecer avatar', 'Rétablir l’avatar', 'アバターを初期化', '기본 아바타로', 'Restaurar avatar', 'Сбросить аватар'],
  'state.on': ['已开启', '已開啟', 'On', 'Activado', 'Activé', 'オン', '켜짐', 'Ativado', 'Вкл.'],
  'state.off': ['已关闭', '已關閉', 'Off', 'Desactivado', 'Désactivé', 'オフ', '꺼짐', 'Desativado', 'Выкл.'],
  'dock.renamePrompt': ['重命名（仅 WebUI 备忘，留空恢复原名）：', '重新命名（僅 WebUI 備忘，留空還原原名）：', 'Rename (WebUI note only; leave empty to restore the original name):', 'Renombrar (solo nota de WebUI; deja vacío para restaurar el nombre original):', 'Renommer (note WebUI uniquement ; laisser vide pour rétablir le nom d’origine) :', '名前を変更（WebUI のメモのみ。空欄で元の名前に戻します）：', '이름 변경 (WebUI 메모 전용, 비워 두면 원래 이름으로 복원):', 'Renomear (apenas nota do WebUI; deixe vazio para restaurar o nome original):', 'Переименовать (только заметка WebUI; оставьте пустым, чтобы вернуть исходное имя):'],
  'prompt.username': ['修改用户名（终端提示符同步更新）：', '修改使用者名稱（終端機提示字元同步更新）：', 'Change username (the terminal prompt updates too):', 'Cambiar nombre de usuario (el prompt de la terminal también cambia):', 'Modifier le nom d’utilisateur (l’invite du terminal est mise à jour) :', 'ユーザー名を変更（ターミナルのプロンプトも更新されます）：', '사용자 이름 변경 (터미널 프롬프트도 함께 바뀝니다):', 'Alterar nome de usuário (o prompt do terminal também muda):', 'Изменить имя пользователя (приглашение терминала тоже обновится):'],
  'confirm.exit': ['确定退出游戏？未保存的配置将丢失（可在退出前先点"保存"）。', '確定結束遊戲？未儲存的設定將遺失（可在結束前先點「儲存」）。', 'Exit the game? Unsaved settings will be lost (click "Save" before exiting to keep them).', '¿Salir del juego? Se perderá la configuración no guardada (pulsa «Guardar» antes de salir).', 'Quitter le jeu ? Les paramètres non enregistrés seront perdus (cliquez sur « Enregistrer » avant de quitter).', 'ゲームを終了しますか？保存していない設定は失われます（終了前に「保存」を押してください）。', '게임을 종료할까요? 저장하지 않은 설정은 사라집니다 (종료 전에 "저장"을 누르세요).', 'Sair do jogo? As configurações não salvas serão perdidas (clique em "Salvar" antes de sair).', 'Выйти из игры? Несохранённые настройки будут потеряны (перед выходом нажмите «Сохранить»).'],
  'error.exitFailed': ['退出指令发送失败：{msg}', '結束指令傳送失敗：{msg}', 'Failed to send the exit command: {msg}', 'No se pudo enviar el comando de salida: {msg}', 'Échec de l’envoi de la commande de sortie : {msg}', '終了コマンドの送信に失敗しました：{msg}', '종료 명령 전송 실패: {msg}', 'Falha ao enviar o comando de saída: {msg}', 'Не удалось отправить команду выхода: {msg}'],
  'error.unknown': ['未知错误', '未知錯誤', 'Unknown error', 'Error desconocido', 'Erreur inconnue', '不明なエラー', '알 수 없는 오류', 'Erro desconhecido', 'Неизвестная ошибка'],

  // ---- 裁切 ----
  'crop.wallpaper': ['裁切壁纸', '裁切桌布', 'Crop wallpaper', 'Recortar fondo', 'Recadrer le fond d’écran', '壁紙をトリミング', '배경화면 자르기', 'Recortar papel de parede', 'Обрезать обои'],
  'crop.avatar': ['裁切头像', '裁切頭像', 'Crop avatar', 'Recortar avatar', 'Recadrer l’avatar', 'アバターをトリミング', '아바타 자르기', 'Recortar avatar', 'Обрезать аватар'],
  'crop.icon': ['裁切应用图标', '裁切應用程式圖示', 'Crop app icon', 'Recortar icono de la aplicación', 'Recadrer l’icône de l’application', 'アプリアイコンをトリミング', '앱 아이콘 자르기', 'Recortar ícone do app', 'Обрезать значок приложения'],

  // ---- 顶栏 ----
  'upd.bubble': ['发现新版本 {tag}，点击查看更新内容', '發現新版本 {tag}，點擊查看更新內容', 'New version {tag} is available. Click to view the changes.', 'Nueva versión {tag} disponible. Haz clic para ver los cambios.', 'Nouvelle version {tag} disponible. Cliquez pour voir les changements.', '新しいバージョン {tag} があります。クリックして更新内容を確認', '새 버전 {tag} 사용 가능. 클릭하여 변경 사항 보기', 'Nova versão {tag} disponível. Clique para ver as mudanças.', 'Доступна новая версия {tag}. Нажмите, чтобы посмотреть изменения.'],
  'upd.newVersionHint': ['发现新版本 {tag}，点击查看', '發現新版本 {tag}，點擊查看', 'New version {tag} available, click to view', 'Nueva versión {tag}, haz clic para ver', 'Nouvelle version {tag}, cliquez pour voir', '新しいバージョン {tag}、クリックで確認', '새 버전 {tag}, 클릭하여 보기', 'Nova versão {tag}, clique para ver', 'Новая версия {tag}, нажмите, чтобы посмотреть'],
  'upd.dismiss': ['知道了', '知道了', 'Got it', 'Entendido', 'Compris', 'わかりました', '확인', 'Entendi', 'Понятно'],
  'search.placeholder': ['搜索应用 / 功能…', '搜尋應用程式 / 功能…', 'Search apps / features…', 'Buscar apps / funciones…', 'Rechercher applications / fonctions…', 'アプリ / 機能を検索…', '앱 / 기능 검색…', 'Pesquisar apps / recursos…', 'Поиск приложений / функций…'],
  'search.empty': ['无匹配项。', '無符合項目。', 'No matches.', 'Sin coincidencias.', 'Aucun résultat.', '一致する項目がありません。', '일치하는 항목이 없습니다.', 'Nenhum resultado.', 'Нет совпадений.'],
  'tile.on': ['平铺模式已开启：点此回到可拖动窗口', '平鋪模式已開啟：點此回到可拖曳視窗', 'Tiled mode on: click to return to draggable windows', 'Modo mosaico activado: haz clic para volver a ventanas arrastrables', 'Mode mosaïque activé : cliquez pour revenir aux fenêtres mobiles', 'タイル表示モード オン：クリックで自由配置に戻ります', '타일 모드 켜짐: 클릭하면 자유 배치 창으로 돌아갑니다', 'Modo mosaico ativado: clique para voltar às janelas arrastáveis', 'Режим плитки включён: нажмите, чтобы вернуться к перетаскиваемым окнам'],
  'tile.off': ['平铺模式：窗口铺满工作区，标签页式切换', '平鋪模式：視窗鋪滿工作區，以分頁方式切換', 'Tiled mode: windows fill the workspace and switch like tabs', 'Modo mosaico: las ventanas ocupan el espacio de trabajo y cambian como pestañas', 'Mode mosaïque : les fenêtres remplissent l’espace et s’alternent comme des onglets', 'タイル表示モード：ウィンドウを作業領域いっぱいに並べ、タブで切り替えます', '타일 모드: 창이 작업 영역을 채우고 탭처럼 전환됩니다', 'Modo mosaico: as janelas ocupam o espaço de trabalho e alternam como abas', 'Режим плитки: окна заполняют рабочую область и переключаются как вкладки'],
  'conn.online': ['已连接', '已連線', 'Connected', 'Conectado', 'Connecté', '接続済み', '연결됨', 'Conectado', 'Подключено'],
  'conn.connecting': ['连接中…', '連線中…', 'Connecting…', 'Conectando…', 'Connexion…', '接続中…', '연결 중…', 'Conectando…', 'Подключение…'],
  'window.focus': ['切换前置', '切換至前景', 'Bring to front', 'Traer al frente', 'Mettre au premier plan', '前面に表示', '맨 앞으로', 'Trazer para frente', 'На передний план'],
  'window.closeWindow': ['关闭窗口', '關閉視窗', 'Close window', 'Cerrar ventana', 'Fermer la fenêtre', 'ウィンドウを閉じる', '창 닫기', 'Fechar janela', 'Закрыть окно'],
  'cat.app': ['应用', '應用程式', 'Apps', 'Aplicaciones', 'Applications', 'アプリ', '앱', 'Aplicativos', 'Приложения'],
  'cat.widget': ['小组件', '小工具', 'Widgets', 'Widgets', 'Widgets', 'ウィジェット', '위젯', 'Widgets', 'Виджеты'],
  'cat.system': ['系统', '系統', 'System', 'Sistema', 'Système', 'システム', '시스템', 'Sistema', 'Система'],
  'about.menu': ['关于 DT_Tools…', '關於 DT_Tools…', 'About DT_Tools…', 'Acerca de DT_Tools…', 'À propos de DT_Tools…', 'DT_Tools について…', 'DT_Tools 정보…', 'Sobre o DT_Tools…', 'О DT_Tools…'],

  // ---- 关于弹窗 ----
  'about.product': ['Deadly Trick 游戏工具插件', 'Deadly Trick 遊戲工具外掛', 'Deadly Trick game tools plugin', 'Plugin de herramientas para Deadly Trick', 'Plugin d’outils pour Deadly Trick', 'Deadly Trick ゲームツールプラグイン', 'Deadly Trick 게임 도구 플러그인', 'Plugin de ferramentas do Deadly Trick', 'Плагин инструментов для Deadly Trick'],
  'about.version': ['当前版本 v{v}', '目前版本 v{v}', 'Version v{v}', 'Versión v{v}', 'Version v{v}', 'バージョン v{v}', '버전 v{v}', 'Versão v{v}', 'Версия v{v}'],
  'about.check': ['检查更新', '檢查更新', 'Check for updates', 'Buscar actualizaciones', 'Rechercher les mises à jour', '更新を確認', '업데이트 확인', 'Verificar atualizações', 'Проверить обновления'],
  'about.checkFailed': ['检查失败：{msg}', '檢查失敗：{msg}', 'Check failed: {msg}', 'Error al comprobar: {msg}', 'Échec de la vérification : {msg}', '確認に失敗しました：{msg}', '확인 실패: {msg}', 'Falha na verificação: {msg}', 'Ошибка проверки: {msg}'],
  'about.newVersion': ['发现新版本 {tag}', '發現新版本 {tag}', 'New version {tag} available', 'Nueva versión {tag} disponible', 'Nouvelle version {tag} disponible', '新しいバージョン {tag} があります', '새 버전 {tag} 사용 가능', 'Nova versão {tag} disponível', 'Доступна новая версия {tag}'],
  'about.viewDetails': ['查看详情', '查看詳情', 'View details', 'Ver detalles', 'Voir les détails', '詳細を見る', '자세히 보기', 'Ver detalhes', 'Подробнее'],
  'about.upToDate': ['已是最新版本', '已是最新版本', 'You’re up to date', 'Ya tienes la última versión', 'Vous êtes à jour', '最新バージョンです', '최신 버전입니다', 'Você está atualizado', 'Установлена последняя версия'],
  'about.storage': ['所有配置 / 重命名 / 壁纸 / 图标仅存于本浏览器。', '所有設定 / 重新命名 / 桌布 / 圖示僅儲存於本瀏覽器。', 'Settings, names, wallpaper and icons are stored only in this browser.', 'La configuración, los nombres, el fondo y los iconos solo se guardan en este navegador.', 'Les paramètres, noms, fonds et icônes ne sont enregistrés que dans ce navigateur.', '設定・名前・壁紙・アイコンはこのブラウザにのみ保存されます。', '설정, 이름, 배경화면, 아이콘은 이 브라우저에만 저장됩니다.', 'Configurações, nomes, papel de parede e ícones ficam salvos apenas neste navegador.', 'Настройки, имена, обои и значки хранятся только в этом браузере.'],
  'about.themeRefs': ['主题参考：', '主題參考：', 'Theme references:', 'Referencias de temas:', 'Références des thèmes :', 'テーマの参考：', '테마 참고:', 'Referências de tema:', 'Источники тем:'],

  // ---- 设置 ----
  'settings.followBrowser': ['跟随浏览器', '跟隨瀏覽器', 'Follow browser', 'Seguir el navegador', 'Suivre le navigateur', 'ブラウザに従う', '브라우저 설정 따르기', 'Seguir o navegador', 'Как в браузере'],
  'settings.followBrowserHint': ['当前浏览器语言：{lang}', '目前瀏覽器語言：{lang}', 'Current browser language: {lang}', 'Idioma actual del navegador: {lang}', 'Langue actuelle du navigateur : {lang}', '現在のブラウザの言語：{lang}', '현재 브라우저 언어: {lang}', 'Idioma atual do navegador: {lang}', 'Текущий язык браузера: {lang}'],
  'settings.storeHint': ['语言设置仅保存在本浏览器。游戏内的内容仍以游戏语言显示。', '語言設定僅儲存於本瀏覽器。遊戲內的內容仍以遊戲語言顯示。', 'The language setting is saved only in this browser. In-game content still uses the game language.', 'La configuración de idioma solo se guarda en este navegador. El contenido del juego sigue en el idioma del juego.', 'Le réglage de langue n’est enregistré que dans ce navigateur. Le contenu du jeu reste dans la langue du jeu.', '言語の設定はこのブラウザにのみ保存されます。ゲーム内の内容はゲームの言語で表示されます。', '언어 설정은 이 브라우저에만 저장됩니다. 게임 내 콘텐츠는 게임 언어로 표시됩니다.', 'A configuração de idioma fica salva apenas neste navegador. O conteúdo do jogo continua no idioma do jogo.', 'Настройка языка сохраняется только в этом браузере. Содержимое игры по-прежнему отображается на языке игры.'],

  // ---- 软件更新 ----
  'update.title': ['软件更新', '軟體更新', 'Software update', 'Actualización de software', 'Mise à jour du logiciel', 'ソフトウェアの更新', '소프트웨어 업데이트', 'Atualização de software', 'Обновление ПО'],
  'update.current': ['当前版本', '目前版本', 'Current version', 'Versión actual', 'Version actuelle', '現在のバージョン', '현재 버전', 'Versão atual', 'Текущая версия'],
  'update.latest': ['最新版本', '最新版本', 'Latest version', 'Última versión', 'Dernière version', '最新バージョン', '최신 버전', 'Versão mais recente', 'Последняя версия'],
  'update.newFound': ['发现新版本！发布于 {date}', '發現新版本！發布於 {date}', 'New version found! Released {date}', '¡Nueva versión encontrada! Publicada el {date}', 'Nouvelle version disponible ! Publiée le {date}', '新しいバージョンがあります！公開日: {date}', '새 버전이 있습니다! 게시일: {date}', 'Nova versão encontrada! Lançada em {date}', 'Найдена новая версия! Выпущена {date}'],
  'update.isLatest': ['当前已是最新版本。', '目前已是最新版本。', 'You’re on the latest version.', 'Ya tienes la versión más reciente.', 'Vous utilisez la dernière version.', '最新バージョンを使用中です。', '최신 버전을 사용 중입니다.', 'Você está na versão mais recente.', 'У вас последняя версия.'],
  'update.checkFailed': ['检查失败：{msg}', '檢查失敗：{msg}', 'Check failed: {msg}', 'Error al comprobar: {msg}', 'Échec de la vérification : {msg}', '確認に失敗しました：{msg}', '확인 실패: {msg}', 'Falha na verificação: {msg}', 'Ошибка проверки: {msg}'],
  'update.notChecked': ['尚未检测过更新，点下方"重新检查"。', '尚未檢查過更新，請點下方「重新檢查」。', 'Not checked yet. Click "Check again" below.', 'Aún no se ha comprobado. Pulsa «Volver a comprobar» abajo.', 'Pas encore vérifié. Cliquez sur « Revérifier » ci-dessous.', 'まだ確認していません。下の「再確認」を押してください。', '아직 확인하지 않았습니다. 아래 "다시 확인"을 누르세요.', 'Ainda não verificado. Clique em "Verificar novamente" abaixo.', 'Обновления ещё не проверялись. Нажмите «Проверить снова» ниже.'],
  'update.notes': ['更新说明', '更新說明', 'Release notes', 'Notas de la versión', 'Notes de version', 'リリースノート', '릴리스 노트', 'Notas da versão', 'Примечания к выпуску'],
  'update.download': ['下载新版本', '下載新版本', 'Download new version', 'Descargar nueva versión', 'Télécharger la nouvelle version', '新しいバージョンをダウンロード', '새 버전 다운로드', 'Baixar nova versão', 'Скачать новую версию'],
  'update.downloading': ['下载中…', '下載中…', 'Downloading…', 'Descargando…', 'Téléchargement…', 'ダウンロード中…', '다운로드 중…', 'Baixando…', 'Загрузка…'],
  'update.savedTo': ['已保存到：{path}', '已儲存到：{path}', 'Saved to: {path}', 'Guardado en: {path}', 'Enregistré dans : {path}', '保存先：{path}', '저장 위치: {path}', 'Salvo em: {path}', 'Сохранено в: {path}'],
  'update.openFolder': ['打开所在文件夹', '開啟所在資料夾', 'Open containing folder', 'Abrir carpeta contenedora', 'Ouvrir le dossier', 'フォルダーを開く', '폴더 열기', 'Abrir pasta', 'Открыть папку'],
  'update.downloadFailed': ['下载失败：{msg}', '下載失敗：{msg}', 'Download failed: {msg}', 'Error de descarga: {msg}', 'Échec du téléchargement : {msg}', 'ダウンロードに失敗しました：{msg}', '다운로드 실패: {msg}', 'Falha no download: {msg}', 'Ошибка загрузки: {msg}'],
  'update.recheck': ['重新检查', '重新檢查', 'Check again', 'Volver a comprobar', 'Revérifier', '再確認', '다시 확인', 'Verificar novamente', 'Проверить снова'],
  'update.releasePage': ['前往发布页', '前往發佈頁', 'Go to release page', 'Ir a la página de versiones', 'Aller à la page de la version', 'リリースページへ', '릴리스 페이지로 이동', 'Ir para a página de versões', 'Перейти на страницу выпуска'],
  'update.lastChecked': ['上次检测：{date}（每 3 分钟自动检测一次）', '上次檢測：{date}（每 3 分鐘自動檢測一次）', 'Last checked: {date} (checks automatically every 3 minutes)', 'Última comprobación: {date} (se comprueba cada 3 minutos)', 'Dernière vérification : {date} (vérification automatique toutes les 3 minutes)', '前回の確認: {date}（3 分ごとに自動確認）', '마지막 확인: {date} (3분마다 자동 확인)', 'Última verificação: {date} (verifica a cada 3 minutos)', 'Последняя проверка: {date} (автоматически каждые 3 минуты)'],

  // ---- 小组件 ----
  'steam.title': ['《Deadly Trick》在线', '《Deadly Trick》在線', '《Deadly Trick》 online', '《Deadly Trick》 en línea', '《Deadly Trick》 en ligne', '《Deadly Trick》 オンライン', '《Deadly Trick》 접속자', '《Deadly Trick》 online', '《Deadly Trick》 онлайн'],
  'steam.sub': ['Steam 全球在线玩家 · 每分钟刷新', 'Steam 全球在線玩家 · 每分鐘更新', 'Steam players worldwide · refreshes every minute', 'Jugadores en Steam en todo el mundo · se actualiza cada minuto', 'Joueurs Steam dans le monde · actualisé chaque minute', 'Steam 全世界のオンラインプレイヤー · 毎分更新', '전 세계 Steam 접속자 · 1분마다 갱신', 'Jogadores no Steam no mundo · atualiza a cada minuto', 'Игроки Steam по всему миру · обновляется каждую минуту'],
  'mcp.running': ['运行中', '執行中', 'Running', 'En ejecución', 'En cours', '実行中', '실행 중', 'Em execução', 'Работает'],
  'mcp.disabled': ['已停用', '已停用', 'Disabled', 'Desactivado', 'Désactivé', '無効', '비활성화', 'Desativado', 'Отключён'],
  'mcp.stop': ['停用', '停用', 'Disable', 'Desactivar', 'Désactiver', '無効にする', '비활성화', 'Desativar', 'Отключить'],
  'mcp.start': ['启用', '啟用', 'Enable', 'Activar', 'Activer', '有効にする', '활성화', 'Ativar', 'Включить'],
  'mcp.copy': ['复制接入配置', '複製接入設定', 'Copy connection config', 'Copiar configuración de conexión', 'Copier la configuration de connexion', '接続設定をコピー', '연결 설정 복사', 'Copiar configuração de conexão', 'Копировать настройки подключения'],
  'mcp.copied': ['已复制', '已複製', 'Copied', 'Copiado', 'Copié', 'コピーしました', '복사됨', 'Copiado', 'Скопировано'],
  'mcp.cfgTitle': ['监听配置（与「DT 配置」页同步）', '監聽設定（與「DT 設定」頁同步）', 'Listener settings (synced with the DT Config page)', 'Configuración de escucha (sincronizada con DT Config)', 'Paramètres d’écoute (synchronisés avec DT Config)', '待ち受け設定（DT 設定ページと同期）', '수신 설정 (DT 설정 페이지와 동기화)', 'Configuração de escuta (sincronizada com DT Config)', 'Настройки прослушивания (синхронизированы со страницей DT Config)'],
  'mcp.port': ['端口', '連接埠', 'Port', 'Puerto', 'Port', 'ポート', '포트', 'Porta', 'Порт'],
  'mcp.listenIp': ['监听 IP', '監聽 IP', 'Listen IP', 'IP de escucha', 'IP d’écoute', '待ち受け IP', '수신 IP', 'IP de escuta', 'IP прослушивания'],
  'mcp.password': ['密码', '密碼', 'Password', 'Contraseña', 'Mot de passe', 'パスワード', '비밀번호', 'Senha', 'Пароль'],
  'mcp.passwordPh': ['空=免密', '空=免密碼', 'Empty = no password', 'Vacío = sin contraseña', 'Vide = sans mot de passe', '空欄 = パスワードなし', '비워 두면 암호 없음', 'Vazio = sem senha', 'Пусто = без пароля'],
  'mcp.restartHint': ['监听参数已改，重启游戏后生效', '監聽參數已變更，重新啟動遊戲後生效', 'Listener settings changed. Restart the game to apply them.', 'Parámetros de escucha cambiados. Reinicia el juego para aplicarlos.', 'Paramètres modifiés. Redémarrez le jeu pour les appliquer.', '待ち受け設定を変更しました。ゲームを再起動すると反映されます。', '수신 설정이 변경되었습니다. 게임을 재시작하면 적용됩니다.', 'Parâmetros alterados. Reinicie o jogo para aplicar.', 'Параметры изменены. Перезапустите игру, чтобы применить.'],
  'mcp.listenerOff': ['监听未拉起（启动时已停用）：可经 WebConsole 端口 /mcp 接入，或重启游戏', '監聽未啟動（啟動時已停用）：可經 WebConsole 連接埠 /mcp 接入，或重新啟動遊戲', 'The listener is not running (disabled at startup). Connect via WebConsole port /mcp, or restart the game.', 'El servidor de escucha no está activo (desactivado al iniciar). Conéctate por el puerto /mcp de WebConsole o reinicia el juego.', 'L’écouteur n’est pas lancé (désactivé au démarrage) : connexion via le port /mcp de WebConsole, ou redémarrez le jeu.', '待ち受けは起動していません（起動時に無効）。WebConsole のポート /mcp から接続するか、ゲームを再起動してください。', '수신기가 시작되지 않았습니다 (시작 시 비활성화됨). WebConsole 포트 /mcp로 연결하거나 게임을 재시작하세요.', 'Ouvinte não iniciado (desativado na inicialização): conecte pela porta /mcp do WebConsole ou reinicie o jogo.', 'Прослушивание не запущено (отключено при старте): подключитесь через порт /mcp WebConsole или перезапустите игру.'],
  'mcp.toolsLine': ['工具 {tools} 个 · 调用 {calls} 次', '工具 {tools} 個 · 呼叫 {calls} 次', '{tools} tools · {calls} calls', '{tools} herramientas · {calls} llamadas', '{tools} outils · {calls} appels', 'ツール {tools} 件 · 呼び出し {calls} 回', '도구 {tools}개 · 호출 {calls}회', '{tools} ferramentas · {calls} chamadas', 'Инструментов: {tools} · вызовов: {calls}'],
  'mcp.logEmpty': ['暂无调用（AI 每次工具调用都会记录在此）', '暫無呼叫（AI 每次工具呼叫都會記錄在此）', 'No calls yet (every AI tool call is logged here)', 'Sin llamadas todavía (cada llamada de herramienta de la IA aparece aquí)', 'Aucun appel pour l’instant (chaque appel d’outil de l’IA est journalisé ici)', 'まだ呼び出しはありません（AI のツール呼び出しはすべてここに記録されます）', '아직 호출이 없습니다 (AI 도구 호출은 모두 여기에 기록됩니다)', 'Nenhuma chamada ainda (cada chamada de ferramenta da IA é registrada aqui)', 'Вызовов пока нет (каждый вызов инструмента ИИ записывается здесь)'],
  'news.short': ['游戏新闻', '遊戲新聞', 'News', 'Noticias', 'Actualités', 'ニュース', '뉴스', 'Notícias', 'Новости'],
  'news.error': ['新闻加载失败，请稍后刷新', '新聞載入失敗，請稍後重新整理', 'Failed to load news. Please refresh later.', 'No se pudieron cargar las noticias. Vuelve a intentarlo más tarde.', 'Échec du chargement des actualités. Réessayez plus tard.', 'ニュースの読み込みに失敗しました。しばらくしてから更新してください。', '뉴스를 불러오지 못했습니다. 잠시 후 새로고침하세요.', 'Falha ao carregar as notícias. Atualize mais tarde.', 'Не удалось загрузить новости. Обновите позже.'],
  'news.empty': ['暂无公告', '暫無公告', 'No announcements yet', 'Sin anuncios por ahora', 'Aucune annonce pour le moment', 'お知らせはありません', '공지가 없습니다', 'Nenhum anúncio por enquanto', 'Объявлений пока нет'],
  'news.fallback': ['该语言暂无公告，已显示英文', '此語言暫無公告，已顯示英文', 'No announcements in this language. Showing English.', 'No hay anuncios en este idioma. Se muestra en inglés.', 'Aucune annonce dans cette langue. Affichage en anglais.', 'この言語のお知らせはありません。英語を表示しています。', '이 언어의 공지가 없어 영어로 표시합니다.', 'Sem anúncios neste idioma. Exibindo em inglês.', 'Объявлений на этом языке нет. Показан английский.'],
  'news.source': ['Steam 新闻 · 每 1 分钟刷新', 'Steam 新聞 · 每 1 分鐘更新', 'Steam News · refreshes every minute', 'Noticias de Steam · se actualiza cada minuto', 'Actualités Steam · actualisées chaque minute', 'Steam ニュース · 毎分更新', 'Steam 뉴스 · 1분마다 갱신', 'Notícias da Steam · atualiza a cada minuto', 'Новости Steam · обновляются каждую минуту'],

  // ---- Markdown ----
  'markdown.image': ['图片：', '圖片：', 'Image: ', 'Imagen: ', 'Image : ', '画像：', '이미지: ', 'Imagem: ', 'Изображение: '],
};

/** 按列构建每个语言的词典：{ 'zh-CN': { key: text }, ... }。 */
export function buildMessages() {
  const out = Object.fromEntries(COLS.map((c) => [c, {}]));
  for (const [key, cells] of Object.entries(ROWS)) {
    COLS.forEach((c, i) => { out[c][key] = cells[i]; });
  }
  return out;
}

export const MESSAGE_COLUMNS = COLS;
export const MESSAGE_ROWS = ROWS;
