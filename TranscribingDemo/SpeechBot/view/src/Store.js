import { EventEmitter } from 'events';

class Store extends EventEmitter {
  state = 'main_page'
  response = ''
  values = {}
}

const store = new Store();

store.setMaxListeners(0);

export default store;