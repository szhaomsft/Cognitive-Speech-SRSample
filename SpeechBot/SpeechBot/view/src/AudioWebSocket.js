var websocket = null;
var textDisplay = '';

import { toQueryString } from './utility';

function connect(cb, wsServicePath = 'srprod' , config = {}) {
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
    connect(() => {
      websocket.send('begin');
      console.log('WS opened ' + performance.now());
      audioRecorder.sendHeader(websocket);
      audioRecorder.record(websocket);
      open_cb();
    }, wsServicePath, config);

    websocket.onerror = function (event) {
      if (error_cb) {
        error_cb(this, event);
      }
    };

    websocket.onmessage = function (event) {
      console.log('WS onmessage ' + performance.now());
      var data = event.data;
      if (data == null || data.length <= 0) {
        return;
      }

      // console.log(data);
      message_cb(this, data);
    };

    websocket.onclose = function (event) {
      console.log('WS closed ' + performance.now());
      // stopRecording();
      close_cb(this, event);
      websocket = null;
    };
  },

  stop: function () {
    if (websocket) {
      // websocket.onmessage = function() {};
      // websocket.onerror = function() {};
      // websocket.onclose = function() {};
      // websocket.close();
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
