import { Dispatcher } from 'flux';
let dispatcher = new Dispatcher();

import store from './Store';

// Register callback with dispatcher
dispatcher.register((payload) => {
  switch (payload.type) {
    case 'new-response':
      store.response = payload.text;
      store.emit('response-change');
      break;
    case 'state_transfer':
      store.state = payload.nextState;
      store.emit('state-change');
      break;
    case 'fill_value':
      store.values[payload.controlId] = payload.value;
      store.emit(payload.controlId + '-change');
      break;
    default:
      return true;
  }

  return true;
});

export function handleActions(actions) {
  if (actions) {
    for (var action of actions) {
      dispatcher.dispatch(action);
    }
  }
}

export function handleResponse({action, text}) {
  dispatcher.dispatch({
    type: 'new-response',
    text
  });
  handleActions(action);
}

export default dispatcher;
