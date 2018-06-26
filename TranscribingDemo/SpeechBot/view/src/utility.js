export function toQueryString(obj) {
  let qs = '';
  if (obj != null) {
    for (var key in obj) {
      if (!obj[key]) continue;
      if (qs.length > 0) qs += '&';
      qs += key + '=' + encodeURIComponent(obj[key]);
    }
  }
  return qs;
}