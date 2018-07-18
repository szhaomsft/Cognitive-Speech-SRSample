var websocket = null;
var textDisplay = '';

var l_open_cb;
var l_message_cb;
var l_close_cb;
var l_error_cb;
var l_ws_service_path;
var l_config;
var l_audio_recoder;

import { toQueryString } from './utility';

function connect(cb, wsServicePath = 'speech' , config = {}) {
  try {
    let wsProtocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
    if (!websocket || websocket.readyState !== websocket.OPEN) {
      var uri = `${wsProtocol}//${window.location.hostname}${window.location.port ? ':' + window.location.port : ''}/${wsServicePath}`;
      if (window.location.search.indexOf('mooncakesr') >= 0) {
        uri = `${wsProtocol}//${window.location.hostname}${window.location.port ? ':' + window.location.port : ''}/sr`;
      }
      uri += '?' + toQueryString(config);
      websocket = new WebSocket(uri);
      websocket.onopen = cb;
    } else if (cb) {
      cb();
    }
  } catch(e) {
    console.log('WS connect failed');
    console.log(e);
  }
}

export default {
  connect: connect,

  start: function (audioRecorder, open_cb, message_cb, close_cb, error_cb, wsServicePath, config) {
    l_open_cb = open_cb;
    l_message_cb = message_cb;
    l_close_cb = close_cb;
    l_error_cb = error_cb;
    l_ws_service_path = wsServicePath;
    l_config = config;
    console.log('websocket handler set.' + wsServicePath);
    if (websocket != null && websocket.readyState === websocket.OPEN)
    {
      websocket.onerror = function (event) {
        if (l_error_cb) {
          l_error_cb(this, event);
        }
      };
      console.log("websocket established or re-established.");
      websocket.onmessage = function (event) {
        console.log('WS onmessage ' + performance.now());
        var data = event.data;
        if (data == null || data.length <= 0) {
          return;
        }

        // console.log(data);
        l_message_cb(this, data);
      };

      websocket.onclose = function (event) {
        console.log('WS closed ' + performance.now());
        // stopRecording();
        l_close_cb(this, event);
        websocket = null;
      };
    }
  },

  audio_start: function(audioRecorder){
    // audioRecorder.sendHeader(websocket);
    audioRecorder.record(websocket);
    console.log("recorder websocket set.")
  },

  set_recorder: function(recorder){
    l_audio_recoder = recorder;
    if (websocket != null)
    {
      // l_audio_recoder.sendHeader(websocket);
      l_audio_recoder.record(websocket);
    }
  },

  execute: function (execute_cb)
  {
    if (websocket && websocket.readyState === websocket.OPEN) {
      if (execute_cb){
        execute_cb(this)
      }
    }
    else {
      connect(() => {
        console.log('WS opened ' + performance.now());
        l_open_cb();
        execute_cb(this);
        // l_audio_recoder.sendHeader(websocket);
        l_audio_recoder.record(websocket);
      }, l_ws_service_path, l_config);

      websocket.onerror = function (event) {
        if (l_error_cb) {
          l_error_cb(this, event);
        }
      };
      console.log("websocket established or re-established.");
      websocket.onmessage = function (event) {
        console.log('WS onmessage ' + performance.now());
        var data = event.data;
        if (data == null || data.length <= 0) {
          return;
        }

        // console.log(data);
        l_message_cb(this, data);
      };

      websocket.onclose = function (event) {
        console.log('WS closed ' + performance.now());
        // stopRecording();
        l_close_cb(this, event);
        websocket = null;
      };
    }
  },

  send: function(mes){
    this.execute((a) => {
      websocket.send(mes);
    })
  },

  stop: function () {
    if (websocket) {
      if (websocket != null) {
        websocket.send('end');
      }
    }
  },

  close: function () {
    if (websocket != null) {
      websocket.close();
    }
  }
};
