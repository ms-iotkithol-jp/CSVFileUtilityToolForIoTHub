# CSV file utility tool for Azure IoT Hub
手元にあるCSVで保存されたセンサー等の時系列データを、Azure IoT Hub、Time Series Insights の仕組みを使って、クラウドで時系列データを扱うのに適した、Apache Paquet フォーマットで Blob Storage に保存するための、ユーティリティツール。  
本ツールで動作する Azure IoT Hub と接続可能なエミュレーターが、CSVファイルに記録されたデータを解釈し、Azure IoT Hub に登録されたデバイスとして、データを逐次送信する。  
とりあえず手元にCSVファイルがあって、とりあえず IoT を試してみたい方用に作ってみた。  

大抵の手元にある CSV ファイルには、”どのデバイス”のデータかというデバイス識別子的な情報は記録されていない、あるいは、等間隔で計測しているので計測時間情報も CSV ファイルの各行には保存されていないケースが十分に考えられるので、IoT Hub でデータを受信する際のデバイス識別子（deviceid）と、Cloud 側で保管する際に必要なデータレコードへのタイムスタンプ(timestamp)を追加する機能を有する。デバイス識別子、タイムスタンプが CSV ファイルの各行に存在する場合は、それを指定することも可能である。  
![tool_overview](./images/tool_overview.svg)  
![connect_to_tsi](./images/connect_to_tsi.svg)  

IoT Hub へデータを送信する際のデータフォーマットも選択可能。  
|フォーマット|説明|
|-|-|
|json|CSV の各行を、JSON形式に変換して送信|
|csv|CSV の各行を、CSV の先頭行（カラム名）を毎回先頭につけて送信|

CSV の各行を都度都度送信、複数行まとめて送信のどちらかを選択できる。CSVファイルの Azure IoT Hub への送信のボトルネックは、インターネットを介した、本ツールと Azure IoT Hub との通信であるので、複数行を一括送信したほうが時間的に早い。  
一方で、ツールを実行する PＣ のメモリ量や CPU の性能にもよると思われるので、Azure IoT Hub へのテレメトリー送信の可能最大データサイズの 256KB 以下で設定可能にしている。複数行一括送信を指定した場合は、JSONの場合は、JSON の配列形式で、 CSV の場合は、先頭行が元のCSVファイルの先頭行を1行目に、以下送信する対象の行を順次加えて送信する。  
ネットワークの転送レートが低い場合は、更に、JSON、CSVともにGZIP圧縮して送信するという機能も備えている。  

Azure IoT Hub にメッセージを送信する際、本ツールからの送信であることを示すために、"application":"csv-translator" というアプリケーションプロパティを付与して送信する。  
また、送信フォーマットを明示するために、"format"というアプリケーションプロパティも付与する。値は以下の通り  
|フォーマット|プロパティの値|
|-|-|
|json、非圧縮|json|
|json、圧縮|json-gzip|
|csv、非圧縮|csv|
|csv、圧縮|csv-gzip|

Cloud 側で Azure IoT Hub に送信されたデータの処理は送信フォーマットによって変えなければならないため、2種類以上のフォーマットを並行して使う場合は、図のように、[Azure IoT Hub にカスタムエンドポイントを付与](https://docs.microsoft.com/azure/iot-hub/iot-hub-devguide-messages-read-custom)し、適切なフィルターを設定すればよい。  
例えば、json-gzip のみを受信するカスタムエンドポイントの場合のフィルターは、  
```sql
application = 'csv-translator' AND format = 'json-gzip'
```
とすればよい。  

---

## 実行方法  
本ツールを実行するには、Azure IoT Hub が必要である。既に実験用に Azure IoT Hub インスタンスがある場合は、適切なカスタムエンドポイントを追加して、それを使ってもよい。  
ない場合は、[IoT Hub の作成](https://docs.microsoft.com/azure/iot-hub/quickstart-send-telemetry-dotnet#create-an-iot-hub)を参考に作成して、IoT Device を一つ登録する。  
IoT Device の登録が完了したら、Visual Studio で WpfAppIoTCSVTranslator を開き、登録した IoT Device の接続文字列を、[tool/WpfAppIoTCSVTranslator/WpfAppIoTCSVTranslator/appsettings.json](tool/WpfAppIoTCSVTranslator/WpfAppIoTCSVTranslator/appsettings.json) の "<- device connection string for IoT Hub ->" にコピペする。  
これでツールの設定は完了。  

### Time Series Insights の設定  
Azure IoT Hub のカスタムエンドポイントを使う場合は、[Azure Time Series Insghts 環境にイベントハブイベントソースを追加する](https://docs.microsoft.com/ja-jp/azure/time-series-insights/how-to-ingest-data-event-hub) を参考に、Time Series Insights Event Source の作成を行う。  
Azure IoT Hub に直接つなぐ場合は、[Azure Time Series Insghts 環境に IoT Hub イベントソースを追加する](https://docs.microsoft.com/ja-jp/azure/time-series-insights/how-to-ingest-data-iot-hub) を参考にする。  
次に、[Azure Time Series Insights Gen 2 環境を設定する](https://docs.microsoft.com/azure/time-series-insights/tutorial-set-up-environment)  を参考に、Time Series Insights Gen 2 環境を構築する。  
※ 以上の準備は、送信フォーマットが JSON 非圧縮の場合である。それ以外の場合は、 Stream Analytics を使って、データの解凍や、CSV→JSON変換を行うのが一番簡単なのでおすすめ。  

---
## 使い方 - CSV の各カラムの定義の生成 
### 1. CSV ファイルの選択  
![step1](images/step1_SelectCSV.svg)  
"Select CSV File" をクリックして、送信したい CSV ファイルを選択する。  
"Load CSV Files in a Folder" チェックボックスにチェックを入れてからボタンをクリックすると、一連のデータが連続して保存されたCSVファイルを一括で送信可能。  

### 2. CSV ファイルのパースと各カラムの設定調整  
![step2](images/step2_ParseAndEdit.svg)  
"Parse CSV Defnition" をクリックする。右横上のリストボックスに各カラムの値を元に推定したデータ型（Schema）等が表示される。  
特定のカラムをデバイス識別子や、タイムスタンプとして指定したい場合は、対応するチェックボックスをチェックして指示する。  
※ タイムスタンプとして指定可能なのは、ISO8601で規定された日時文字列のみ  

### 3. CSV カラムの名前の成型  
![step3](images/step3_TranslateColumns.svg)  
"TranslateColumnName" をクリックすると、CSVファイルの1行目の文字列の先頭・末尾のスペースの削除、含まれる、スペース文字を"_"に変換する。  

### 4. DTDL 生成用の定義入力  
![step4](images/step4_SettingDTDL.svg)  
IoT Hub の PnP 定義生成のための情報を付与する。  
"Model" の "@id"の"<- user definition ->"の部分を各自の用途に合わせて編集する。  
"Model" の "Display Name"を適切な値に修正する。 
その他のパートも必要に応じて修正する。  

### 5. DTDL 生成  
![step5](images/step5_GenerateDTDL.svg)  
"Generate DTDL" をクリックすると、DTDLに則った形式で、PnP モデルが表示される。  
CSV ファイルの各行の送信用定義と、Device Id、タイムスタンプの定義が表示される。  

### 6. DTDL 保存  
![step6](images/step6_SaveDTDL.svg)  
"Save DTDL"をクリックして、生成されたPnPモデルを保存する。  

以上で CSV の各カラムの定義指定は完了。  

## 使い方 - CSV ファイルの送信  
前項の Step 1. で説明した方法で、CSV ファイル、もしくは CSV ファイル群が格納されているフォルダーを選択する。  
既に、PnP DTDL を生成＆保存済みの場合は、次のステップでデータを送信する。  
### 1. DTDL の選択  
![step11](images/step11_SelectDTDL.svg)  
"Select DTDL File" をクリックする。既に CSV ファイルからの PnP DTDL ファイルを生成している場合には、Alert ダイアログが表示されるので、"OK" をクリックする。  

### 2. IoT Hub への接続  
![step12](images/step12_ConnectToIoTHub.svg)  
"Connect to IoT Hub" をクリックすると、IoT Hub にappsettings.json に設定した接続文字列で接続される。  
"Use Model Id" チェックボックスにチェックを入れてからクリックすると、PnP DTDL ファイルの modelId を伴っての接続を行う。  

### 3. 送信方法の設定  
![step13](images/step13_SetSendingConfig.svg)  
送信フォーマット、送信間隔、1行/複数行、最大データサイズ、圧縮・非圧縮、タイムスタンプの時間指定 等を行う。  

### 4. タイムスタンプの設定  
![step14](images/step14_SetTimestamp.svg)  
"Data Time" にチェックを入れない場合は、本ツールがデータを送信するときのタイムスタンプが付与される。（Single Line選択時のみ利用を推奨） 
"Data Time" にチェックを入れると、"Start Time" と "Delta(msec)" が有効になり、最初のデータを送るときのタイムスタンプと、以降のデータ間隔がこの二つで指定可能である。  
CSVファイルをフォルダーで選択している場合は、継続したタイムスタンプが付与される。  
### 5. IoT Hub へのデータ送信  
"Start"　をクリックすると送信が開始される。  

