﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNetWebToolkit.Cil2Js.JsResolvers.Classes {
    static class _Enumerable {

        #region Count

        public static int Count<TSource>(this IEnumerable<TSource> source) {
            var sourceCollection = source as ICollection;
            if (sourceCollection != null) {
                return sourceCollection.Count;
            }
            int count = 0;
            foreach (var item in source) {
                count++;
            }
            return count;
        }

        public static int Count<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate) {
            int count = 0;
            foreach (var item in source) {
                if (predicate(item)) {
                    count++;
                }
            }
            return count;
        }

        #endregion

        #region Distinct

        public static IEnumerable<TSource> Distinct<TSource>(this IEnumerable<TSource> source) {
            return source.Distinct(null);
        }

        public static IEnumerable<TSource> Distinct<TSource>(this IEnumerable<TSource> source, IEqualityComparer<TSource> comparer) {
            var hs = new HashSet<TSource>(comparer);
            foreach (var item in source) {
                if (hs.Add(item)) {
                    yield return item;
                }
            }
        }

        #endregion

        #region First

        public static TSource First<TSource>(this IEnumerable<TSource> source) {
            foreach (var item in source) {
                return item;
            }
            throw new InvalidOperationException();
        }

        public static TSource First<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate) {
            foreach (var item in source) {
                if (predicate(item)) {
                    return item;
                }
            }
            throw new InvalidOperationException();
        }

        public static TSource FirstOrDefault<TSource>(this IEnumerable<TSource> source) {
            foreach (var item in source) {
                return item;
            }
            return default(TSource);
        }

        public static TSource FirstOrDefault<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate) {
            foreach (var item in source) {
                if (predicate(item)) {
                    return item;
                }
            }
            return default(TSource);
        }

        #endregion

        #region OrderBy, OrderByDescending

        #endregion

        #region Select

        public static IEnumerable<TResult> Select<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, TResult> selector) {
            foreach (var item in source) {
                yield return selector(item);
            }
        }

        public static IEnumerable<TResult> Select<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, int, TResult> selector) {
            int i = 0;
            foreach (var item in source) {
                yield return selector(item, i);
                i++;
            }
        }

        #endregion Select

        #region SelectMany

        public static IEnumerable<TResult> SelectMany<TSource, TResult>
            (this IEnumerable<TSource> source, Func<TSource, IEnumerable<TResult>> selector) {
            foreach (var item in source) {
                foreach (var innerItem in selector(item)) {
                    yield return innerItem;
                }
            }
        }

        public static IEnumerable<TResult> SelectMany<TSource, TResult>
            (this IEnumerable<TSource> source, Func<TSource, int, IEnumerable<TResult>> selector) {
            int i = 0;
            foreach (var item in source) {
                foreach (var innerItem in selector(item, i)) {
                    yield return innerItem;
                }
                i++;
            }
        }

        public static IEnumerable<TResult> SelectMany<TSource, TCollection, TResult>
            (this IEnumerable<TSource> source, Func<TSource, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector) {
            foreach (var item in source) {
                foreach (var innerItem in collectionSelector(item)) {
                    yield return resultSelector(item, innerItem);
                }
            }
        }

        public static IEnumerable<TResult> SelectMany<TSource, TCollection, TResult>
            (this IEnumerable<TSource> source, Func<TSource, int, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector) {
            int i = 0;
            foreach (var item in source) {
                foreach (var innerItem in collectionSelector(item, i)) {
                    yield return resultSelector(item, innerItem);
                }
                i++;
            }
        }

        #endregion

        #region Sum

        public static int Sum(this IEnumerable<int> source) {
            int sum = 0;
            foreach (var item in source) {
                sum += item;
            }
            return sum;
        }

        public static double Sum(this IEnumerable<double> source) {
            double sum = 0;
            foreach (var item in source) {
                sum += item;
            }
            return sum;
        }

        #endregion

        #region Where

        public static IEnumerable<T> Where<T>(this IEnumerable<T> source, Func<T, bool> predicate) {
            foreach (var item in source) {
                if (predicate(item)) {
                    yield return item;
                }
            }
        }

        public static IEnumerable<T> Where<T>(this IEnumerable<T> source, Func<T, int, bool> predicate) {
            int i = 0;
            foreach (var item in source) {
                if (predicate(item, i)) {
                    yield return item;
                }
                i++;
            }
        }

        #endregion

        #region ToArray, ToList, ToDictionary

        public static T[] ToArray<T>(this IEnumerable<T> source) {
            return new List<T>(source).ToArray();
        }

        public static List<T> ToList<T>(this IEnumerable<T> source) {
            return new List<T>(source);
        }

        public static Dictionary<TKey, TSource> ToDictionary<TSource, TKey>
            (this IEnumerable<TSource> source, Func<TSource, TKey> keySelector) {
            var d = new Dictionary<TKey, TSource>();
            foreach (var item in source) {
                d.Add(keySelector(item), item);
            }
            return d;
        }

        public static Dictionary<TKey, TSource> ToDictionary<TSource, TKey>
            (this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey> comparer) {
            var d = new Dictionary<TKey, TSource>(comparer);
            foreach (var item in source) {
                d.Add(keySelector(item), item);
            }
            return d;
        }

        public static Dictionary<TKey, TElement> ToDictionary<TSource, TKey, TElement>
            (this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector) {
            var d = new Dictionary<TKey, TElement>();
            foreach (var item in source) {
                d.Add(keySelector(item), elementSelector(item));
            }
            return d;
        }

        public static Dictionary<TKey, TElement> ToDictionary<TSource, TKey, TElement>
            (this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer) {
            var d = new Dictionary<TKey, TElement>(comparer);
            foreach (var item in source) {
                d.Add(keySelector(item), elementSelector(item));
            }
            return d;
        }

        #endregion

    }
}
