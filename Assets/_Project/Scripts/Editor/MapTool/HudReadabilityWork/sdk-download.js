/**
 *  Copyright(c) Live2D Inc. All rights reserved.
 */

// ベースディレクトリ。FQDNで表す。例: "https://cubism.live2d.com/sdk-web/bin/"
let FILE_BASE_URL = "https://cubism.live2d.com/sdk-unity/bin/";

// ダウンロード実行するURL
let DOWNLOAD_URL = "./sdk_dl/"

window.addEventListener("load", () => {

    const USER_STATUS = {
        install: "new",
        update: "update"
    }
    const USER_TYPE = {
        individual: "個人",
        enterprise: "法人"
    }

    const LATEST_URP_VERSION_KEY_NAME = '5-r.6.1';
    const LATEST_URP_BETA_VERSION_KEY_NAME = '';
    const LATEST_BIRP_VERSION_KEY_NAME = '5-r.4.2';
    const LATEST_BIRP_BETA_VERSION_KEY_NAME = '';

    const NOTICE = ``

    // 登録しているファイルのリスト
    /*
        {ID名}: {                                       // ファイルを表すID名。
            date: "2019/04/24",                         // 更新日時。
            size: "24.5MB",                             // ファイルサイズ。
            version: "beta1",                           // ファイルのリビジョン。例: beta1, R1
            url: "CubismSdkForWeb-3-r.1.zip"            // ファイルのURL。
        }
     */
    // URP版
    const fileUrpDataArray = {
        '5-r.6.1': {
            date: "2026-10-29",
            version: "Cubism 5 SDK for Unity R6_1",
            url: "CubismSdkForUnity-5-r.6.1.unitypackage",
        },
        '5-r.6': {
            date: "2026-10-20",
            version: "Cubism 5 SDK for Unity R6",
            url: "CubismSdkForUnity-5-r.6.unitypackage",
        },
        '5-r.5': {
            date: "2026-04-02",
            version: "Cubism 5 SDK for Unity R5",
            url: "CubismSdkForUnity-5-r.5.unitypackage",
        },
        '5-r.5-beta.3': {
            date: "2026-01-08",
            version: "Cubism 5 SDK for Unity R5 beta3",
            url: "CubismSdkForUnity-5-r.5-beta.3.unitypackage",
        },
    }

    // BiRP版
    const fileBirpDataArray = {
        '5-r.4.2': {
            date: "2026-05-14",
            version: "Cubism 5 SDK for Unity R4_2",
            url: "CubismSdkForUnity-5-r.4.2.unitypackage",
        },
        '5-r.5-beta.2': {
            date: "2025-10-14",
            version: "Cubism 5 SDK for Unity R5 beta2",
            url: "CubismSdkForUnity-5-r.5-beta.2.unitypackage",
        },
        '5-r.5-beta.1': {
            date: "2025-08-26",
            version: "Cubism 5 SDK for Unity R5 beta1",
            url: "CubismSdkForUnity-5-r.5-beta.1.unitypackage",
        },
        '5-r.4.1': {
            date: "2025-07-17",
            version: "Cubism 5 SDK for Unity R4_1",
            url: "CubismSdkForUnity-5-r.4.1.unitypackage",
        },
        '5-r.4': {
            date: "2025-05-29",
            version: "Cubism 5 SDK for Unity R4",
            url: "CubismSdkForUnity-5-r.4.unitypackage",
        },
        '5-r.3': {
            date: "2024-11-28",
            version: "Cubism 5 SDK for Unity R3",
            url: "CubismSdkForUnity-5-r.3.unitypackage",
        },
        '5-r.2': {
            date: "2024-04-04",
            version: "Cubism 5 SDK for Unity R2",
            url: "CubismSdkForUnity-5-r.2.unitypackage",
        },
        '5-r.1': {
            date: "2024-03-26",
            version: "Cubism 5 SDK for Unity R1",
            url: "CubismSdkForUnity-5-r.1.unitypackage",
        },
        '5-r.1-beta.4': {
            date: "2024-01-18",
            version: "Cubism 5 SDK for Unity R1 beta4",
            url: "CubismSdkForUnity-5-r.1-beta.4.unitypackage",
        },
        '5-r.1-beta.3': {
            date: "2023-11-14",
            version: "Cubism 5 SDK for Unity R1 beta3",
            url: "CubismSdkForUnity-5-r.1-beta.3.unitypackage",
        },
        '5-r.1-beta.2': {
            date: "2023-09-28",
            version: "Cubism 5 SDK for Unity R1 beta2",
            url: "CubismSdkForUnity-5-r.1-beta.2.unitypackage",
        },
        '5-r.1-beta.1': {
            date: "2023-08-17",
            version: "Cubism 5 SDK for Unity R1 beta1",
            url: "CubismSdkForUnity-5-r.1-beta.1.unitypackage",
        },
        '4-r.7': {
            date: "2023-05-25",
            version: "Cubism 4 SDK for Unity R7",
            url: "CubismSdkForUnity-4-r.7.unitypackage",
        },
        '4-r.6.2': {
            date: "2023-03-16",
            version: "Cubism 4 SDK for Unity R6_2",
            url: "CubismSdkForUnity-4-r.6.2.unitypackage",
        },
        '4-r.6.1': {
            date: "2023-03-10",
            version: "Cubism 4 SDK for Unity R6_1",
            url: "CubismSdkForUnity-4-r.6.1.unitypackage",
        },
        '4-r.6': {
            date: "2023-01-26",
            version: "Cubism 4 SDK for Unity R6",
            url: "CubismSdkForUnity-4-r.6.unitypackage",
        },
        '4-r.5': {
            date: "2022-09-08",
            version: "Cubism 4 SDK for Unity R5",
            url: "CubismSdkForUnity-4-r.5.unitypackage",
        },
        '4-r.5-beta.5': {
            date: "2022-08-04",
            version: "Cubism 4 SDK for Unity R5 beta5",
            url: "CubismSdkForUnity-4-r.5-beta.5.unitypackage",
        },
        '4-r.5-beta.4': {
            date: "2022-07-07",
            version: "Cubism 4 SDK for Unity R5 beta4",
            url: "CubismSdkForUnity-4-r.5-beta.4.unitypackage",
        },
        '4-r.5-beta.3': {
            date: "2022-06-16",
            version: "Cubism 4 SDK for Unity R5 beta3",
            url: "CubismSdkForUnity-4-r.5-beta.3.unitypackage",
        },
        '4-r.5-beta.2': {
            date: "2022-06-02",
            version: "Cubism 4 SDK for Unity R5 beta2",
            url: "CubismSdkForUnity-4-r.5-beta.2.unitypackage",
        },
        '4-r.5-beta.1': {
            date: "2022-05-19",
            version: "Cubism 4 SDK for Unity R5 beta1",
            url: "CubismSdkForUnity-4-r.5-beta.1.unitypackage",
        },
        '4-r.4.2': {
            date: "2022-03-09",
            version: "Cubism 4 SDK for Unity R4_2",
            url: "CubismSdkForUnity-4-r.4.2.unitypackage",
        },
        '4-r.4.1': {
            date: "2022-02-10",
            version: "Cubism 4 SDK for Unity R4_1",
            url: "CubismSdkForUnity-4-r.4.1.unitypackage",
        },
        '4-r.4': {
            date: "2021-12-09",
            version: "Cubism 4 SDK for Unity R4",
            url: "CubismSdkForUnity-4-r.4.unitypackage",
        },
        '4-r.3': {
            date: "2021-06-10",
            version: "Cubism 4 SDK for Unity R3",
            url: "CubismSdkForUnity-4-r.3.unitypackage",
        },
        '4-r.2': {
            date: "2021-01-12",
            version: "Cubism 4 SDK for Unity R2",
            url: "CubismSdkForUnity-4-r.2.unitypackage",
        },
        '4-r.1': {
            date: "2020-01-30",
            version: "Cubism 4 SDK for Unity R1",
            url: "CubismSdkForUnity-4-r.1.unitypackage",
        },
        '4-beta.2': {
            date: "2019-11-14",
            version: "Cubism 4 SDK for Unity beta2",
            url: "CubismSdkForUnity-4-beta.2.unitypackage",
        },
        '4-beta.1': {
            date: "2019-09-04",
            version: "Cubism 4 SDK for Unity beta1",
            url: "CubismSdkForUnity-4-beta.1.unitypackage",
        },
    }

    // -----------------------------

    //==============ダウンロードリンクの有効無効制御===============
    let isMailValid = false;
    let dltype = "";
    let usertype = "";
    const isInstall = () => dltype === "install"
    const isUpdate = () => dltype === "update"

    // 言語設定
    const editorLang = getLanguageCode(document.documentElement.lang)
    let manualLang = ""
    if (editorLang == "zh") {
        manualLang = "zh-CHS/"
    } else if (editorLang != "jp") {
        manualLang = editorLang + "/"
    }

    // 更新履歴・Update Hisrotyのhtmlを自動生成
    // 「更新履歴」の文言は、元々の p タグに要素として埋め込まれている。各言語ごとに存在するので、都度参照し設定する。
    const historyUrl = `https://docs.live2d.com/${manualLang}cubism-sdk-manual/cubism-sdk-for-unity/#changelog`
    if (LATEST_URP_VERSION_KEY_NAME != "") {
        let updateHistoryText = $("#update_history_urp").text()
        let fileInfo = fileUrpDataArray[LATEST_URP_VERSION_KEY_NAME]
        setText("update_history_urp", `<a href="${historyUrl}">${updateHistoryText}</a>  [${fileInfo['date']}] ${fileInfo['version']}`)
    }

    if (LATEST_URP_BETA_VERSION_KEY_NAME != "") {
        let updateHistoryBetaText = $("#update_history_beta_urp").text()
        let fileInfo = fileUrpDataArray[LATEST_URP_BETA_VERSION_KEY_NAME]
        setText("update_history_beta_urp", `<a href="${historyUrl}">${updateHistoryBetaText}</a>  [${fileInfo['date']}] ${fileInfo['version']}`)
    }

    if (LATEST_BIRP_VERSION_KEY_NAME != "") {
        let updateHistoryText = $("#update_history_birp").text()
        let fileInfo = fileBirpDataArray[LATEST_BIRP_VERSION_KEY_NAME]
        setText("update_history_birp", `<a href="${historyUrl}">${updateHistoryText}</a>  [${fileInfo['date']}] ${fileInfo['version']}`)
        // BiRP版は正式版があるときbetaを非表示
        getField('accept_beta_birp').parentElement.parentElement.style.display = 'none'
    }

    if (LATEST_BIRP_BETA_VERSION_KEY_NAME != "") {
        let updateHistoryBetaText = $("#update_history_beta_birp").text()
        let fileInfo = fileBirpDataArray[LATEST_BIRP_BETA_VERSION_KEY_NAME]
        setText("update_history_beta_birp", `<a href="${historyUrl}">${updateHistoryBetaText}</a>  [${fileInfo['date']}] ${fileInfo['version']}`)
    }

    if (NOTICE != "") {
        setText("notice", NOTICE)
    }

    // 画面イベントの設定
    getField("change").addEventListener("change", e => fieldChanged(e), false)
    const update = getField("update")
    update.addEventListener("change", e => fieldChanged(e), false)
    update.checked = true
    getField("firstdl").addEventListener("change", e => fieldChanged(e), false)

    const usertypes = Array.from(document.getElementsByName("usertype"))
    usertypes.forEach(elm =>
        elm.addEventListener("change", () => userTypeFieldChanged(), false)
    )

    function userTypeFieldChanged() {
        const usertypeElm = usertypes.find(elm => elm.checked)
        usertype = (usertypeElm != null) ? usertypeElm.value : ""
    }

    // メールアドレス入力情報を取得
    const $mailForm = $('#mc-embedded-subscribe-form');
    const email_entry = getField("mce-EMAIL");
    $mailForm.validate({
        rules: {
            EMAIL: {
                required: false
            }
        },
        onkeyup: function(b, c) {
            if($mailForm.valid() && email_entry.value !== "") {
                mailValidSuccess();
            }else{
                mailInvalid();
            }

            fieldChanged();
        },
        success: function(label) {
            mailValidSuccess();
        },
        errorPlacement: function(form, validator){
            mailInvalid();
        }
    });

    function mailValidSuccess() {
        isMailValid = true;
    }

    function mailInvalid() {
        isMailValid = false;
    }

    // フォームのEnable/Disableの初期化
    fieldChanged();

    // フィールドが変更された場合に処理する関数
    function fieldChanged(event) {
        const isAgreementChecked = document.forms.agreement.accept.checked;

        const dltypes = Array.from(document.getElementsByName("dltype"))
        dltype = dltypes.find(elm => elm.checked).value

        const requireMailAdress = isAgreementChecked && isInstall()
        email_entry.disabled = !requireMailAdress
        usertypes.forEach(elm => elm.disabled = !requireMailAdress)

        if (isInstall()) {
            // 「初めてダウンロードする」に切り替えたときだけ個人法人のラジオボタンに初期値をセット
            if(event != null && (event.target.id === "firstdl" || event.target.for === "firstdl")) {
                getField("individual").checked = true
                userTypeFieldChanged()
            }
            $("#usertype_select").show()
            $('#mc-embedded-subscribe-form').show()
        } else if (isUpdate()) {
            // 入力の初期化
            usertypes.forEach(elm => elm.checked = false)
            email_entry.value = ""
            isMailValid = false
            $("#usertype_select").hide()
            $('#mc-embedded-subscribe-form').hide()
        }

        const accept_urp = getField("accept_urp");
        const accept_urp_beta = getField("accept_beta_urp");
        const accept_birp = getField("accept_birp");
        const accept_birp_beta = getField("accept_beta_birp");

        if(isAgreementChecked){
            dltypes.forEach(elm => elm.disabled = false)
            if (isUpdate() || (isInstall() && isMailValid)) {
                activateDlButton()
            } else {
                deactivateDlButton()
            }
        } else {
            dltypes.forEach(elm => elm.disabled = true)
            deactivateDlButton()
        }

        if (LATEST_URP_VERSION_KEY_NAME == "") {
            accept_urp.disabled = true
            setText("update_history_urp", "");
        }

        if (LATEST_URP_BETA_VERSION_KEY_NAME == "") {
            accept_urp_beta.disabled = true
            setText("update_history_beta_urp", "");
        }

        if (LATEST_BIRP_VERSION_KEY_NAME == "") {
            accept_birp.disabled = true
            setText("update_history_birp", "");
        }

        if (LATEST_BIRP_BETA_VERSION_KEY_NAME == "") {
            accept_birp_beta.disabled = true
            setText("update_history_beta_birp", "");
        }

        function activateDlButton() {
            // DLボタンを表示
            accept_urp.disabled = false
            accept_urp_beta.disabled = false
            accept_birp.disabled = false
            accept_birp_beta.disabled = false
        }
        function deactivateDlButton(){
            // DLボタンを非表示
            accept_urp.disabled = true
            accept_urp_beta.disabled = true
            accept_birp.disabled = true
            accept_birp_beta.disabled = true
        }
    }

    // SDKパッケージのダウンロードボタンの設定。
    getField("accept_urp").addEventListener("click", function() {
        execUrpDownload(LATEST_URP_VERSION_KEY_NAME);
    });

    getField("accept_beta_urp").addEventListener("click", function() {
        execUrpDownload(LATEST_URP_BETA_VERSION_KEY_NAME);
    });

    getField("accept_birp").addEventListener("click", function() {
        execBirpDownload(LATEST_BIRP_VERSION_KEY_NAME);
    });

    getField("accept_beta_birp").addEventListener("click", function() {
        execBirpDownload(LATEST_BIRP_BETA_VERSION_KEY_NAME);
    });

    function execUrpDownload(version) {
        execDownload(version, FILE_BASE_URL + fileUrpDataArray[version]['url'])
    }

    function execBirpDownload(version) {
        execDownload(version, FILE_BASE_URL + fileBirpDataArray[version]['url'])
    }

    function execDownload(version, fileUrl) {
        // GAパラメータ送信
        const gaParam = {
            'event': 'cubism_sdk_download',
            'sdk_type': 'Unity',
            'user_status': USER_STATUS[dltype],
            'user_type': isInstall() ? USER_TYPE[usertype] : "",
            'version': version
        }
        sendGA(gaParam)

        const logParam = [
            'event=cubism_sdk_download',
            'sdk_type=' + 'Unity',
            'user_status=' + USER_STATUS[dltype],
            'user_type=' + (isInstall() ? USER_TYPE[usertype] : ""),
            'version=' + version,
            'lang=' + document.documentElement.lang,
        ]

        // ダウンロード実行
        const url = fileUrl + `?${logParam.join('&')}`
        location.href = DOWNLOAD_URL + `?url=${encodeURIComponent(url)}`
    }
}, false)

