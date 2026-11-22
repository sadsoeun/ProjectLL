using System.Collections.Generic;
namespace MyGame
{
    public static class UiTextKeys
    {
        public static void Register(Dictionary<string,string[]> t)
        {
            Add(t,"ui.audio","AUDIO","声音","오디오","SONIDO","オーディオ");
            Add(t,"ui.display","DISPLAY","画面","화면","PANTALLA","画面");
            Add(t,"ui.on","On","开启","켜짐","Sí","オン");
            Add(t,"ui.off","Off","关闭","꺼짐","No","オフ");
            Add(t,"ui.settings_note","Changes apply immediately and save when closed.","更改立即生效，关闭时保存。","변경 사항은 즉시 적용되며 닫을 때 저장됩니다.","Los cambios se aplican al instante y se guardan al cerrar.","変更はすぐに反映され、閉じると保存されます。");
            Add(t,"ui.language_note","Select a language. Back returns to settings.","选择语言，返回设置。","언어를 선택하면 설정으로 돌아갑니다.","Selecciona un idioma para volver a los ajustes.","言語を選ぶと設定に戻ります。");
            Add(t,"ui.pause_note","Retry resets this attempt's time and collectibles.","重新开始会重置本次时间和收集品。","다시 시작하면 이번 도전의 시간과 수집품이 초기화됩니다.","Reintentar reinicia el tiempo y los objetos de este intento.","リトライすると今回の時間と収集品がリセットされます。");
            Add(t,"ui.recovery_note","Your lives will be refilled whichever you choose.","无论选择哪项，生命都会恢复。","어느 것을 선택해도 목숨이 다시 채워집니다.","Elijas lo que elijas, tus vidas se recargarán.","どれを選んでも残機は回復します。");
            Add(t,"ui.save_settings_failed","Settings could not be saved. Retry saving?","设置保存失败。重试保存？","설정을 저장하지 못했습니다. 다시 저장할까요?","No se pudieron guardar los ajustes. ¿Reintentar?","設定を保存できませんでした。再試行しますか？");
            Add(t,"ui.save_progress_failed","Progress could not be saved. Retry saving?","进度保存失败。重试保存？","진행 상황을 저장하지 못했습니다. 다시 저장할까요?","No se pudo guardar el progreso. ¿Reintentar?","進行状況を保存できませんでした。再試行しますか？");
            Add(t,"ui.saved","Saved.","已保存。","저장했습니다.","Guardado.","保存しました。");
            Add(t,"ui.save_notice","Save failed. Open the menu to retry.","保存失败，请打开菜单重试。","저장에 실패했습니다. 메뉴에서 다시 저장할 수 있습니다.","Error al guardar. Abre el menú para reintentar.","保存に失敗しました。メニューから再試行できます。");
            Add(t,"ui.retry","Retry saving","重试保存","다시 저장","Reintentar","再保存");
            Add(t,"ui.dismiss","Close without saving","不保存并关闭","저장 없이 닫기","Cerrar sin guardar","保存せず閉じる");
            Add(t,"ui.cancel","Cancel","取消","취소","Cancelar","キャンセル");
            Add(t,"ui.loading_failed","This stage could not be loaded. Returning to the world map.","无法加载关卡，返回世界地图。","스테이지를 불러오지 못해 월드맵으로 돌아갑니다.","No se pudo cargar el nivel. Volviendo al mapa.","ステージを読み込めないため、ワールドマップに戻ります。");
            Add(t,"ui.new_record","NEW BEST","新纪录","신기록","NUEVO RÉCORD","新記録");
            Add(t,"ui.result_master","MASTER!","完美通关！","마스터!","¡MAESTRO!","マスター！");
            Add(t,"ui.result_heading","RESULTS","通关结果","클리어 결과","RESULTADOS","クリア結果");
            Add(t,"ui.result_coins","Coins","金币","코인","Monedas","コイン");
            Add(t,"ui.result_specials","Golden leaves","金色树叶","황금 나뭇잎","Hojas doradas","黄金の葉");
            Add(t,"ui.result_loot","Coins {0}   ·   Golden leaves {1}/{2}","金币 {0}   ·   金色树叶 {1}/{2}","코인 {0}   ·   황금 나뭇잎 {1}/{2}","Monedas {0}   ·   Hojas doradas {1}/{2}","コイン {0}   ·   黄金の葉 {1}/{2}");
        }
        private static void Add(Dictionary<string,string[]> t,string key,params string[] values)=>t[key]=values;
    }
}
